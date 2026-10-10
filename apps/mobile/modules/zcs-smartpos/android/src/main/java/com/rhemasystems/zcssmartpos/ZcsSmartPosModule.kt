package com.rhemasystems.zcssmartpos

import android.os.SystemClock
import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition
import java.lang.reflect.Method

class ZcsSmartPosModule : Module() {
  private val bridge = ZcsSdkReflectionBridge()

  override fun definition() = ModuleDefinition {
    Name("ZcsSmartPos")

    AsyncFunction("getCapabilities") {
      bridge.capabilities()
    }

    AsyncFunction("initialize") {
      bridge.initialize()
    }

    AsyncFunction("printText") { text: String, options: Map<String, Any?>? ->
      bridge.printText(
        text,
        (options?.get("textSize") as? Number)?.toInt() ?: 24,
        (options?.get("feedLines") as? Number)?.toInt() ?: 4
      )
    }

    AsyncFunction("powerOnScanner") {
      bridge.powerOnScanner()
    }

    AsyncFunction("triggerScanner") {
      bridge.triggerScanner()
    }

    AsyncFunction("stopScanner") {
      bridge.stopScanner()
    }

    AsyncFunction("powerOffScanner") {
      bridge.powerOffScanner()
    }
  }
}

/**
 * SmartPos 1.8.1 is supplied as a proprietary JAR/JNI pair without detected
 * redistribution terms. Reflection keeps ordinary development builds healthy
 * while the config plugin packages the externally supplied SDK for Z92S builds.
 */
internal class ZcsSdkReflectionBridge {
  private var driverManager: Any? = null
  private var initialized = false

  fun capabilities(): Map<String, Any?> {
    val sdkAvailable = classAvailable(DRIVER_MANAGER_CLASS)
    return mapOf(
      "nativeModuleAvailable" to true,
      "sdkAvailable" to sdkAvailable,
      "printerAvailable" to (sdkAvailable && methodAvailable(DRIVER_MANAGER_CLASS, "getPrinter")),
      "scannerAvailable" to (sdkAvailable && methodAvailable(DRIVER_MANAGER_CLASS, "getHQrsannerDriver")),
      "detail" to if (sdkAvailable) null else "SmartPos 1.8.1 was not packaged into this Android build."
    )
  }

  @Synchronized
  fun initialize(): Int {
    if (initialized) return 0
    val manager = manager()
    val sys = invoke(manager, "getBaseSysDevice")
    var status = invokeInt(sys, "sdkInit")
    if (status != SDK_OK) {
      invoke(sys, "sysPowerOn")
      SystemClock.sleep(1000)
      status = invokeInt(sys, "sdkInit")
    }
    if (status != SDK_OK) {
      throw ZcsHardwareException("ZCS SDK initialization failed with status $status.")
    }
    initialized = true
    return status
  }

  @Synchronized
  fun printText(text: String, textSize: Int, feedLines: Int): Int {
    require(text.isNotBlank()) { "Receipt text is required." }
    initialize()
    val printer = invoke(manager(), "getPrinter")
    val currentStatus = invokeInt(printer, "getPrinterStatus")
    if (currentStatus != SDK_OK) {
      throw ZcsHardwareException("ZCS printer is not ready (status $currentStatus). Check the paper and printer cover.")
    }

    val format = newInstance("com.zcs.sdk.print.PrnStrFormat")
    invoke(format, "setTextSize", textSize.coerceIn(16, 36))
    setEnum(format, "setFont", "com.zcs.sdk.print.PrnTextFont", "MONOSPACE")
    setEnum(format, "setStyle", "com.zcs.sdk.print.PrnTextStyle", "NORMAL")

    val append = findMethod(printer.javaClass, "setPrintAppendString", 2)
    text.replace("\r\n", "\n").replace('\r', '\n').split('\n').forEach { line ->
      append.invoke(printer, line.ifEmpty { " " }, format)
    }
    repeat(feedLines.coerceIn(1, 8)) { append.invoke(printer, " ", format) }

    val result = invokeInt(printer, "setPrintStart")
    if (result != SDK_OK) {
      throw ZcsHardwareException("ZCS printing failed with status $result.")
    }
    return result
  }

  @Synchronized
  fun powerOnScanner() {
    initialize()
    scannerControl(1)
    scannerPower(0)
    SystemClock.sleep(10)
    scannerPower(1)
  }

  @Synchronized
  fun triggerScanner() {
    initialize()
    scannerControl(1)
    SystemClock.sleep(10)
    scannerControl(0)
  }

  @Synchronized
  fun stopScanner() {
    if (!classAvailable(DRIVER_MANAGER_CLASS)) return
    scannerControl(1)
  }

  @Synchronized
  fun powerOffScanner() {
    if (!classAvailable(DRIVER_MANAGER_CLASS)) return
    scannerPower(0)
  }

  private fun scanner(): Any = invoke(manager(), "getHQrsannerDriver")

  private fun scannerControl(value: Int) {
    invoke(scanner(), "QRScanerCtrl", value.toByte())
  }

  private fun scannerPower(value: Int) {
    invoke(scanner(), "QRScanerPowerCtrl", value.toByte())
  }

  private fun manager(): Any {
    driverManager?.let { return it }
    val type = Class.forName(DRIVER_MANAGER_CLASS)
    val manager = findMethod(type, "getInstance", 0).invoke(null)
      ?: throw ZcsHardwareException("ZCS DriverManager did not return an instance.")
    driverManager = manager
    return manager
  }

  private fun classAvailable(name: String): Boolean = try {
    Class.forName(name)
    true
  } catch (_: ClassNotFoundException) {
    false
  } catch (_: UnsatisfiedLinkError) {
    false
  }

  private fun methodAvailable(className: String, methodName: String): Boolean = try {
    Class.forName(className).methods.any { it.name == methodName }
  } catch (_: Throwable) {
    false
  }

  private fun newInstance(className: String): Any = try {
    Class.forName(className).getDeclaredConstructor().newInstance()
  } catch (error: Throwable) {
    throw ZcsHardwareException("Unable to create $className.", unwrap(error))
  }

  private fun invoke(target: Any, name: String, vararg arguments: Any): Any = try {
    findMethod(target.javaClass, name, arguments.size).invoke(target, *arguments)
      ?: Unit
  } catch (error: Throwable) {
    throw ZcsHardwareException("ZCS call $name failed.", unwrap(error))
  }

  private fun invokeInt(target: Any, name: String): Int {
    val value = invoke(target, name)
    return (value as? Number)?.toInt()
      ?: throw ZcsHardwareException("ZCS call $name did not return a status code.")
  }

  private fun setEnum(target: Any, methodName: String, enumClassName: String, value: String) {
    val enumClass = Class.forName(enumClassName)
    val enumValue = enumClass.enumConstants
      ?.firstOrNull { (it as? Enum<*>)?.name == value }
      ?: throw ZcsHardwareException("ZCS enum $enumClassName.$value is unavailable.")
    invoke(target, methodName, enumValue)
  }

  private fun findMethod(type: Class<*>, name: String, argumentCount: Int): Method =
    type.methods.firstOrNull { it.name == name && it.parameterTypes.size == argumentCount }
      ?: throw ZcsHardwareException("ZCS method ${type.name}.$name/$argumentCount is unavailable.")

  private fun unwrap(error: Throwable): Throwable = error.cause ?: error

  companion object {
    private const val DRIVER_MANAGER_CLASS = "com.zcs.sdk.DriverManager"
    private const val SDK_OK = 0
  }
}

internal class ZcsHardwareException(message: String, cause: Throwable? = null) : RuntimeException(message, cause)

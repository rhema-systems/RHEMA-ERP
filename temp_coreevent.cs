using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Diagnostics;

var fields = typeof(CoreEventId).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .Where(f => f.Name.Contains("Ignored", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Navigation", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f.Name);
foreach (var field in fields)
{
    Console.WriteLine(field.Name);
}

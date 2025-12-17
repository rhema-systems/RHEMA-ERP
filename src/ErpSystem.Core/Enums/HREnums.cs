namespace ErpSystem.Core.Enums;

/// <summary>
/// Gender classification for employees
/// </summary>
public enum Gender
{
    /// <summary>
    /// Male
    /// </summary>
    Male = 1,

    /// <summary>
    /// Female
    /// </summary>
    Female = 2,

    /// <summary>
    /// Other/Non-binary
    /// </summary>
    Other = 3,

    /// <summary>
    /// Prefer not to say
    /// </summary>
    PreferNotToSay = 4
}

/// <summary>
/// Marital status classification
/// </summary>
public enum MaritalStatus
{
    /// <summary>
    /// Single
    /// </summary>
    Single = 1,

    /// <summary>
    /// Married
    /// </summary>
    Married = 2,

    /// <summary>
    /// Divorced
    /// </summary>
    Divorced = 3,

    /// <summary>
    /// Widowed
    /// </summary>
    Widowed = 4,

    /// <summary>
    /// Separated
    /// </summary>
    Separated = 5,

    /// <summary>
    /// Other
    /// </summary>
    Other = 6
}

/// <summary>
/// Employee employment status
/// </summary>
public enum StaffStatus
{
    /// <summary>
    /// Active employee
    /// </summary>
    Active = 1,

    /// <summary>
    /// Inactive employee
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Employee on probation
    /// </summary>
    Probation = 3,

    /// <summary>
    /// Suspended employee
    /// </summary>
    Suspended = 4,

    /// <summary>
    /// Terminated employee
    /// </summary>
    Terminated = 5,

    /// <summary>
    /// Retired employee
    /// </summary>
    Retired = 6,

    /// <summary>
    /// Employee on leave
    /// </summary>
    OnLeave = 7
}

/// <summary>
/// Employment contract types
/// </summary>
public enum ContractType
{
    /// <summary>
    /// Permanent full-time employment
    /// </summary>
    Permanent = 1,

    /// <summary>
    /// Fixed-term contract
    /// </summary>
    Contract = 2,

    /// <summary>
    /// Part-time employment
    /// </summary>
    PartTime = 3,

    /// <summary>
    /// Temporary employment
    /// </summary>
    Temporary = 4,

    /// <summary>
    /// Internship
    /// </summary>
    Internship = 5,

    /// <summary>
    /// Consultancy
    /// </summary>
    Consultant = 6,

    /// <summary>
    /// Freelancer
    /// </summary>
    Freelance = 7
}

/// <summary>
/// Blood type classification
/// </summary>
public enum BloodType
{
    /// <summary>
    /// A positive
    /// </summary>
    APositive = 1,

    /// <summary>
    /// A negative
    /// </summary>
    ANegative = 2,

    /// <summary>
    /// B positive
    /// </summary>
    BPositive = 3,

    /// <summary>
    /// B negative
    /// </summary>
    BNegative = 4,

    /// <summary>
    /// AB positive
    /// </summary>
    ABPositive = 5,

    /// <summary>
    /// AB negative
    /// </summary>
    ABNegative = 6,

    /// <summary>
    /// O positive
    /// </summary>
    OPositive = 7,

    /// <summary>
    /// O negative
    /// </summary>
    ONegative = 8,

    /// <summary>
    /// Unknown blood type
    /// </summary>
    Unknown = 9
}

/// <summary>
/// Employee skill levels
/// </summary>
public enum SkillLevel
{
    /// <summary>
    /// Beginner level
    /// </summary>
    Beginner = 1,

    /// <summary>
    /// Intermediate level
    /// </summary>
    Intermediate = 2,

    /// <summary>
    /// Advanced level
    /// </summary>
    Advanced = 3,

    /// <summary>
    /// Expert level
    /// </summary>
    Expert = 4,

    /// <summary>
    /// Master level
    /// </summary>
    Master = 5
}

/// <summary>
/// Department types for organizational structure
/// </summary>
public enum DepartmentType
{
    /// <summary>
    /// Operations department
    /// </summary>
    Operations = 1,

    /// <summary>
    /// Administration department
    /// </summary>
    Administration = 2,

    /// <summary>
    /// Human Resources department
    /// </summary>
    HumanResources = 3,

    /// <summary>
    /// Finance department
    /// </summary>
    Finance = 4,

    /// <summary>
    /// Information Technology department
    /// </summary>
    IT = 5,

    /// <summary>
    /// Maintenance department
    /// </summary>
    Maintenance = 6,

    /// <summary>
    /// Safety department
    /// </summary>
    Safety = 7,

    /// <summary>
    /// Quality Assurance department
    /// </summary>
    QualityAssurance = 8,

    /// <summary>
    /// Research and Development department
    /// </summary>
    RnD = 9,

    /// <summary>
    /// Marketing department
    /// </summary>
    Marketing = 10,

    /// <summary>
    /// Sales department
    /// </summary>
    Sales = 11
}

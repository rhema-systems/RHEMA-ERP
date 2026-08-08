using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the qualification catalogue for an existing tenant.
/// Idempotent — skips any entry whose Name already exists for that tenant.
/// Run again after adding new entries to pick them up without duplicates.
/// </summary>
public class QualificationSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QualificationSeeder> _logger;

    public QualificationSeeder(ApplicationDbContext context, ILogger<QualificationSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding qualifications for tenant {TenantId}", tenantId);

        // Load existing names once — O(1) lookups thereafter
        var existingList = await _context.Qualifications
            .Where(q => q.TenantId == tenantId)
            .Select(q => q.Name)
            .ToListAsync();
        var existing = existingList.ToHashSet();

        var toAdd = GetAllQualifications(tenantId)
            .Where(q => !existing.Contains(q.Name))
            .ToList();

        if (toAdd.Count == 0)
        {
            _logger.LogInformation("All qualifications already present for tenant {TenantId}. Nothing to insert.", tenantId);
            return;
        }

        await _context.Qualifications.AddRangeAsync(toAdd);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} new qualification(s) for tenant {TenantId}.", toAdd.Count, tenantId);
    }

    private static List<Qualification> GetAllQualifications(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        Qualification Q(string name, string code, QualificationType type, string desc, string? authority = null) => new()
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            Name             = name,
            ShortCode        = code,
            Type             = type,
            Description      = desc,
            IssuingAuthority = authority,
            IsActive         = true,
            CreatedAt        = now,
            CreatedBy        = "Seeder",
        };

        return new List<Qualification>
        {
            // ── Secondary Education ────────────────────────────────────────────
            Q("High School Diploma",                              "HSD",       QualificationType.Education, "Secondary education completion certificate"),
            Q("General Certificate of Education (GCE)",          "GCE",       QualificationType.Education, "British secondary education qualification"),
            Q("West African Senior School Certificate (WASSCE)",  "WASSCE",    QualificationType.Education, "West African secondary education certificate", "WAEC"),
            Q("International Baccalaureate (IB)",                 "IB",        QualificationType.Education, "International pre-university qualification"),
            Q("General Certificate of Secondary Education",       "GCSE",      QualificationType.Education, "UK secondary school qualification"),
            Q("National Senior Certificate (NSC)",                "NSC",       QualificationType.Education, "South African matric certificate", "DBE South Africa"),

            // ── Sub-Degree / Foundation ────────────────────────────────────────
            Q("Ordinary National Diploma",                        "OND",       QualificationType.Education, "Two-year post-secondary technical qualification"),
            Q("Higher National Diploma",                          "HND",       QualificationType.Education, "UK higher education diploma"),
            Q("Associate Degree",                                 "AS/AA",     QualificationType.Education, "Two-year undergraduate qualification (US)"),
            Q("Foundation Certificate",                           "FC",        QualificationType.Education, "Entry-level pre-degree qualification"),
            Q("Diploma in Business Administration",               "DipBA",     QualificationType.Education, "Post-secondary business administration diploma"),
            Q("Diploma in Information Technology",                "DIT",       QualificationType.Education, "Post-secondary IT diploma"),
            Q("Diploma in Engineering",                           "DipEng",    QualificationType.Education, "Post-secondary engineering diploma"),
            Q("Diploma in Nursing",                               "DipN",      QualificationType.Education, "Post-secondary nursing diploma"),
            Q("Diploma in Accounting",                            "DipAcc",    QualificationType.Education, "Post-secondary accounting diploma"),
            Q("Diploma in Education",                             "DipEd",     QualificationType.Education, "Teaching diploma (pre-degree level)"),
            Q("Advanced Diploma in Management",                   "AdvDipMgt", QualificationType.Education, "Advanced management diploma"),

            // ── Bachelor's Degrees ─────────────────────────────────────────────
            Q("Bachelor of Arts",                                 "BA",        QualificationType.Education, "Undergraduate degree in arts and humanities"),
            Q("Bachelor of Science",                              "BSc",       QualificationType.Education, "Undergraduate degree in sciences"),
            Q("Bachelor of Business Administration",              "BBA",       QualificationType.Education, "Undergraduate degree in business administration"),
            Q("Bachelor of Commerce",                             "BCom",      QualificationType.Education, "Undergraduate degree in commerce"),
            Q("Bachelor of Engineering",                          "BEng",      QualificationType.Education, "Undergraduate degree in engineering"),
            Q("Bachelor of Technology",                           "BTech",     QualificationType.Education, "Undergraduate degree in technology"),
            Q("Bachelor of Computer Science",                     "BCS",       QualificationType.Education, "Undergraduate degree in computer science"),
            Q("Bachelor of Information Systems",                  "BIS",       QualificationType.Education, "Undergraduate degree in information systems"),
            Q("Bachelor of Laws",                                 "LLB",       QualificationType.Education, "Undergraduate degree in law"),
            Q("Bachelor of Medicine, Bachelor of Surgery",        "MBBS",      QualificationType.Education, "Medical degree"),
            Q("Bachelor of Nursing",                              "BN",        QualificationType.Education, "Undergraduate degree in nursing"),
            Q("Bachelor of Education",                            "BEd",       QualificationType.Education, "Undergraduate degree in education"),
            Q("Bachelor of Fine Arts",                            "BFA",       QualificationType.Education, "Undergraduate degree in fine arts"),
            Q("Bachelor of Architecture",                         "BArch",     QualificationType.Education, "Undergraduate degree in architecture"),
            Q("Bachelor of Social Work",                          "BSW",       QualificationType.Education, "Undergraduate degree in social work"),
            Q("Bachelor of Public Health",                        "BPH",       QualificationType.Education, "Undergraduate degree in public health"),
            Q("Bachelor of Pharmacy",                             "BPharm",    QualificationType.Education, "Undergraduate degree in pharmacy"),
            Q("Bachelor of Accounting",                           "BAcc",      QualificationType.Education, "Undergraduate degree in accounting"),
            Q("Bachelor of Economics",                            "BEcon",     QualificationType.Education, "Undergraduate degree in economics"),
            Q("Bachelor of Finance",                              "BFin",      QualificationType.Education, "Undergraduate degree in finance"),
            Q("Bachelor of Psychology",                           "BPsych",    QualificationType.Education, "Undergraduate degree in psychology"),
            Q("Bachelor of Social Sciences",                      "BSocSci",   QualificationType.Education, "Undergraduate degree in social sciences"),
            Q("Bachelor of Human Resource Management",            "BHRM",      QualificationType.Education, "Undergraduate degree in human resource management"),
            Q("Bachelor of Supply Chain Management",              "BSCM",      QualificationType.Education, "Undergraduate degree in supply chain management"),

            // ── Master's Degrees ───────────────────────────────────────────────
            Q("Master of Arts",                                   "MA",        QualificationType.Education, "Postgraduate degree in arts and humanities"),
            Q("Master of Science",                                "MSc",       QualificationType.Education, "Postgraduate degree in sciences"),
            Q("Master of Business Administration",                "MBA",       QualificationType.Education, "Postgraduate degree in business administration"),
            Q("Master of Engineering",                            "MEng",      QualificationType.Education, "Postgraduate degree in engineering"),
            Q("Master of Technology",                             "MTech",     QualificationType.Education, "Postgraduate degree in technology"),
            Q("Master of Computer Applications",                  "MCA",       QualificationType.Education, "Postgraduate degree in computer applications"),
            Q("Master of Laws",                                   "LLM",       QualificationType.Education, "Postgraduate degree in law"),
            Q("Master of Education",                              "MEd",       QualificationType.Education, "Postgraduate degree in education"),
            Q("Master of Public Administration",                  "MPA",       QualificationType.Education, "Postgraduate degree in public administration"),
            Q("Master of Philosophy",                             "MPhil",     QualificationType.Education, "Postgraduate research degree"),
            Q("Master of Finance",                                "MFin",      QualificationType.Education, "Postgraduate degree in finance"),
            Q("Master of Public Health",                          "MPH",       QualificationType.Education, "Postgraduate degree in public health"),
            Q("Master of Social Work",                            "MSW",       QualificationType.Education, "Postgraduate degree in social work"),
            Q("Master of Data Science",                           "MDS",       QualificationType.Education, "Postgraduate degree in data science"),
            Q("Master of Supply Chain Management",                "MSCM",      QualificationType.Education, "Postgraduate degree in supply chain management"),
            Q("Master of Architecture",                           "MArch",     QualificationType.Education, "Postgraduate degree in architecture"),
            Q("Master of Information Technology",                 "MIT",       QualificationType.Education, "Postgraduate degree in information technology"),

            // ── Doctoral Degrees ───────────────────────────────────────────────
            Q("Doctor of Philosophy",                             "PhD",       QualificationType.Education, "Highest academic research degree"),
            Q("Doctor of Medicine",                               "MD",        QualificationType.Education, "Medical doctorate"),
            Q("Doctor of Business Administration",                "DBA",       QualificationType.Education, "Doctoral degree in business administration"),
            Q("Doctor of Education",                              "EdD",       QualificationType.Education, "Doctoral degree in education"),
            Q("Doctor of Nursing Practice",                       "DNP",       QualificationType.Education, "Doctoral degree in nursing practice"),
            Q("Doctor of Pharmacy",                               "PharmD",    QualificationType.Education, "Doctoral degree in pharmacy"),

            // ── Accounting & Finance ───────────────────────────────────────────
            Q("Chartered Accountant",                             "CA",        QualificationType.Certification, "Professional accounting qualification"),
            Q("Certified Public Accountant",                      "CPA",       QualificationType.Certification, "US accounting certification", "AICPA"),
            Q("Association of Chartered Certified Accountants",   "ACCA",      QualificationType.Certification, "International accounting qualification", "ACCA"),
            Q("Chartered Institute of Management Accountants",    "CIMA",      QualificationType.Certification, "Management accounting qualification", "CIMA"),
            Q("Chartered Financial Analyst",                      "CFA",       QualificationType.Certification, "Investment management qualification", "CFA Institute"),
            Q("Financial Risk Manager",                           "FRM",       QualificationType.Certification, "Risk management certification", "GARP"),
            Q("Certified Management Accountant",                  "CMA",       QualificationType.Certification, "Management accounting certification", "IMA"),
            Q("Certified Internal Auditor",                       "CIA",       QualificationType.Certification, "Internal auditing certification", "IIA"),
            Q("Certified Fraud Examiner",                         "CFE",       QualificationType.Certification, "Fraud examination certification", "ACFE"),
            Q("Certified Treasury Professional",                  "CTP",       QualificationType.Certification, "Treasury and cash management", "AFP"),
            Q("Certified Risk Manager",                           "CRM",       QualificationType.Certification, "Enterprise risk management certification"),
            Q("Certified in Risk and Information Systems Control","CRISC",     QualificationType.Certification, "IT risk and information systems control", "ISACA"),

            // ── Project Management ─────────────────────────────────────────────
            Q("Project Management Professional",                  "PMP",       QualificationType.Certification, "PMI project management certification", "PMI"),
            Q("PRINCE2 Practitioner",                             "PRINCE2",   QualificationType.Certification, "UK project management methodology", "Axelos"),
            Q("Certified ScrumMaster",                            "CSM",       QualificationType.Certification, "Agile Scrum certification", "Scrum Alliance"),
            Q("Agile Certified Practitioner",                     "PMI-ACP",   QualificationType.Certification, "PMI agile certification", "PMI"),
            Q("Certified Associate in Project Management",        "CAPM",      QualificationType.Certification, "Entry-level PMI certification", "PMI"),
            Q("Managing Successful Programmes",                   "MSP",       QualificationType.Certification, "Programme management qualification", "Axelos"),

            // ── IT – Networking & Infrastructure ──────────────────────────────
            Q("Cisco Certified Network Associate",                "CCNA",      QualificationType.Certification, "Cisco networking certification", "Cisco"),
            Q("Cisco Certified Network Professional",             "CCNP",      QualificationType.Certification, "Advanced Cisco networking certification", "Cisco"),
            Q("CompTIA A+",                                       "A+",        QualificationType.Certification, "IT support and hardware certification", "CompTIA"),
            Q("CompTIA Network+",                                 "Network+",  QualificationType.Certification, "Networking fundamentals certification", "CompTIA"),
            Q("CompTIA Security+",                                "Security+", QualificationType.Certification, "Cybersecurity certification", "CompTIA"),
            Q("CompTIA Linux+",                                   "Linux+",    QualificationType.Certification, "Linux system administration", "CompTIA"),
            Q("Microsoft Certified Solutions Expert",             "MCSE",      QualificationType.Certification, "Microsoft technology certification", "Microsoft"),
            Q("Microsoft Certified: Azure Administrator",         "AZ-104",    QualificationType.Certification, "Microsoft Azure administration", "Microsoft"),
            Q("Microsoft Certified: Azure Developer Associate",   "AZ-204",    QualificationType.Certification, "Microsoft Azure development", "Microsoft"),
            Q("Microsoft Certified: Power Platform Fundamentals", "PL-900",    QualificationType.Certification, "Microsoft Power Platform basics", "Microsoft"),
            Q("VMware Certified Professional",                    "VCP",       QualificationType.Certification, "VMware virtualisation certification", "VMware"),
            Q("Red Hat Certified Engineer",                       "RHCE",      QualificationType.Certification, "Red Hat Linux engineering", "Red Hat"),
            Q("Certified Kubernetes Administrator",               "CKA",       QualificationType.Certification, "Kubernetes cluster administration", "CNCF"),
            Q("Docker Certified Associate",                       "DCA",       QualificationType.Certification, "Docker containerisation certification", "Docker"),
            Q("ITIL 4 Foundation",                                "ITIL4",     QualificationType.Certification, "IT service management framework", "Axelos"),

            // ── IT – Cloud & Data ──────────────────────────────────────────────
            Q("AWS Certified Solutions Architect",                "AWS-SA",    QualificationType.Certification, "Amazon Web Services architecture", "AWS"),
            Q("AWS Certified Developer",                          "AWS-Dev",   QualificationType.Certification, "Amazon Web Services development", "AWS"),
            Q("Google Cloud Professional Cloud Architect",        "GCP-PCA",   QualificationType.Certification, "Google Cloud architecture", "Google"),
            Q("Oracle Database Administrator Certified Professional", "OCA",   QualificationType.Certification, "Oracle database administration", "Oracle"),
            Q("Salesforce Certified Administrator",               "SFDC-ADM",  QualificationType.Certification, "Salesforce CRM administration", "Salesforce"),
            Q("Power BI Data Analyst Associate",                  "PL-300",    QualificationType.Certification, "Microsoft Power BI data analysis", "Microsoft"),
            Q("Tableau Desktop Specialist",                       "TDS",       QualificationType.Certification, "Tableau data visualisation", "Salesforce"),
            Q("SAP Certified Application Associate",              "SAP-CA",    QualificationType.Certification, "SAP ERP application certification", "SAP"),

            // ── Cybersecurity ──────────────────────────────────────────────────
            Q("Certified Information Systems Security Professional", "CISSP",  QualificationType.Certification, "Advanced cybersecurity certification", "ISC2"),
            Q("Certified Information Security Manager",           "CISM",      QualificationType.Certification, "Information security management", "ISACA"),
            Q("Certified Ethical Hacker",                         "CEH",       QualificationType.Certification, "Ethical hacking and penetration testing", "EC-Council"),

            // ── HR & People Management ─────────────────────────────────────────
            Q("Professional in Human Resources",                  "PHR",       QualificationType.Certification, "HR certification", "HRCI"),
            Q("Senior Professional in Human Resources",           "SPHR",      QualificationType.Certification, "Senior HR certification", "HRCI"),
            Q("Chartered Institute of Personnel and Development",  "CIPD",      QualificationType.Certification, "UK HR professional body membership", "CIPD"),
            Q("CIPD Level 3 Foundation Certificate",              "CIPD-L3",   QualificationType.Certification, "Foundation HR and L&D", "CIPD"),
            Q("CIPD Level 5 Associate Diploma",                   "CIPD-L5",   QualificationType.Certification, "Intermediate HR and L&D", "CIPD"),
            Q("CIPD Level 7 Advanced Diploma",                    "CIPD-L7",   QualificationType.Certification, "Advanced HR and L&D", "CIPD"),
            Q("SHRM Certified Professional",                      "SHRM-CP",   QualificationType.Certification, "SHRM HR certification", "SHRM"),
            Q("SHRM Senior Certified Professional",               "SHRM-SCP",  QualificationType.Certification, "SHRM senior HR certification", "SHRM"),

            // ── Legal ──────────────────────────────────────────────────────────
            Q("Barrister-at-Law",                                 "BL",        QualificationType.Certification, "Legal practitioner qualification"),
            Q("Solicitor",                                        "SOL",       QualificationType.Certification, "Legal practitioner qualification"),
            Q("Notary Public",                                    "NP",        QualificationType.Certification, "Public notary qualification"),
            Q("Licensed Conveyancer",                             "LC",        QualificationType.Certification, "Property conveyancing licence"),

            // ── Medical & Healthcare ───────────────────────────────────────────
            Q("Registered Nurse",                                 "RN",        QualificationType.Certification, "Nursing licence"),
            Q("Licensed Practical Nurse",                         "LPN",       QualificationType.Certification, "Practical nursing licence"),
            Q("Certified Nursing Assistant",                      "CNA",       QualificationType.Certification, "Nursing assistant certification"),
            Q("Medical Laboratory Technologist",                  "MLT",       QualificationType.Certification, "Laboratory technologist certification"),
            Q("Registered Pharmacist",                            "RPh",       QualificationType.Certification, "Pharmacy licence"),
            Q("Paramedic Certification",                          "EMT-P",     QualificationType.Certification, "Emergency paramedic qualification"),

            // ── Engineering ────────────────────────────────────────────────────
            Q("Professional Engineer",                            "PE",        QualificationType.Certification, "Engineering licence"),
            Q("Chartered Engineer",                               "CEng",      QualificationType.Certification, "UK engineering qualification"),
            Q("Engineer in Training",                             "EIT",       QualificationType.Certification, "Entry-level engineering certification"),
            Q("Incorporated Engineer",                            "IEng",      QualificationType.Certification, "UK incorporated engineer"),

            // ── Quality & Process Improvement ─────────────────────────────────
            Q("Six Sigma Green Belt",                             "SSGB",      QualificationType.Certification, "Six Sigma process improvement"),
            Q("Six Sigma Black Belt",                             "SSBB",      QualificationType.Certification, "Advanced Six Sigma certification"),
            Q("Six Sigma Master Black Belt",                      "SSMBB",     QualificationType.Certification, "Expert-level Six Sigma"),
            Q("Certified Quality Engineer",                       "CQE",       QualificationType.Certification, "Quality engineering certification", "ASQ"),
            Q("ISO 9001 Lead Auditor",                            "ISO9001",   QualificationType.Certification, "Quality management system lead auditor"),
            Q("ISO 27001 Lead Implementer",                       "ISO27001",  QualificationType.Certification, "Information security management system"),

            // ── Supply Chain & Logistics ───────────────────────────────────────
            Q("Chartered Institute of Procurement and Supply",    "CIPS",      QualificationType.Certification, "Procurement and supply management", "CIPS"),
            Q("Chartered Institute of Logistics and Transport",   "CILT",      QualificationType.Certification, "Logistics and transport qualification", "CILT"),
            Q("Certified Supply Chain Professional",              "CSCP",      QualificationType.Certification, "End-to-end supply chain management", "APICS"),
            Q("Certified in Production and Inventory Management", "CPIM",      QualificationType.Certification, "Production and inventory management", "APICS"),
            Q("Certified Purchasing Manager",                     "CPM",       QualificationType.Certification, "Purchasing management certification", "ISM"),

            // ── Marketing & Sales ──────────────────────────────────────────────
            Q("Chartered Institute of Marketing (CIM) Diploma",   "CIM",       QualificationType.Certification, "Professional marketing qualification", "CIM"),
            Q("Google Analytics Certified",                       "GAIQ",      QualificationType.Certification, "Digital analytics certification", "Google"),
            Q("Google Ads Certification",                         "GAds",      QualificationType.Certification, "Google advertising certification", "Google"),
            Q("HubSpot Inbound Marketing Certification",          "HIM",       QualificationType.Certification, "Inbound marketing certification", "HubSpot"),
            Q("Meta Blueprint Certification",                     "META-BP",   QualificationType.Certification, "Facebook/Instagram advertising", "Meta"),
            Q("Certified Professional Marketer",                  "CPMkt",     QualificationType.Certification, "Marketing certification", "AMA"),

            // ── Health, Safety & Environment ──────────────────────────────────
            Q("NEBOSH General Certificate",                       "NEBOSH-GC", QualificationType.Certification, "Health and safety qualification", "NEBOSH"),
            Q("NEBOSH National Diploma",                          "NEBOSH-ND", QualificationType.Certification, "Advanced health and safety qualification", "NEBOSH"),
            Q("IOSH Managing Safely",                             "IOSH-MS",   QualificationType.Certification, "Safety management for line managers", "IOSH"),
            Q("First Aid at Work (FAW)",                          "FAW",       QualificationType.Certification, "Workplace first aid certification"),
            Q("Emergency First Aid at Work (EFAW)",               "EFAW",      QualificationType.Certification, "Basic workplace emergency first aid"),
            Q("Food Hygiene Certificate Level 2",                 "FH-L2",     QualificationType.Certification, "Basic food safety and hygiene"),
            Q("Food Hygiene Certificate Level 3",                 "FH-L3",     QualificationType.Certification, "Supervisory food safety and hygiene"),

            // ── Vocational / Trade ─────────────────────────────────────────────
            Q("National Vocational Qualification",                "NVQ",       QualificationType.TechnicalSkills, "UK work-based qualification"),
            Q("City & Guilds Certification",                      "C&G",       QualificationType.TechnicalSkills, "UK vocational qualification", "City & Guilds"),
            Q("Trade Certificate",                                "TC",        QualificationType.TechnicalSkills, "Skilled trade certification"),
            Q("Apprenticeship Certificate",                       "AC",        QualificationType.TechnicalSkills, "Apprenticeship completion"),

            // ── Licences ──────────────────────────────────────────────────────
            Q("Driving Licence - Class B (Private)",              "DL-B",      QualificationType.License, "Standard car and light vehicle licence"),
            Q("Commercial Driver's Licence (CDL)",                "CDL",       QualificationType.License, "Heavy goods vehicle / truck licence"),
            Q("Motorcycle Licence",                               "DL-MC",     QualificationType.License, "Motorcycle riding licence"),
            Q("Forklift Operator Licence",                        "FLO",       QualificationType.License, "Forklift operation licence"),
            Q("Private Pilot Licence (PPL)",                      "PPL",       QualificationType.License, "Recreational pilot licence", "CAA"),
            Q("Commercial Pilot Licence (CPL)",                   "CPL",       QualificationType.License, "Commercial aviation pilot licence", "CAA"),
            Q("Electrical Installation Licence",                  "EIL",       QualificationType.License, "Licensed electrical installation work"),
            Q("Building Contractor Licence",                      "BCL",       QualificationType.License, "Licensed building and construction work"),
            Q("Real Estate Licence",                              "REL",       QualificationType.License, "Real estate agency licence"),
            Q("Insurance Broker Licence",                         "IBL",       QualificationType.License, "Licensed insurance brokerage"),
            Q("Medical Licence",                                  "ML",        QualificationType.License, "Licence to practice medicine"),
            Q("Teaching Certificate / Licence",                   "TC-EDU",    QualificationType.License, "Licence to teach in schools"),

            // ── Language Proficiency ───────────────────────────────────────────
            Q("TOEFL - Test of English as a Foreign Language",    "TOEFL",     QualificationType.Language, "English language proficiency test", "ETS"),
            Q("IELTS - International English Language Testing",   "IELTS",     QualificationType.Language, "English language proficiency test", "British Council / IDP"),
            Q("Cambridge English Certificate (CAE)",              "CAE",       QualificationType.Language, "Cambridge C1 Advanced English qualification", "Cambridge"),
            Q("DELF B2 - French Language Proficiency",            "DELF-B2",   QualificationType.Language, "French B2 proficiency", "CIEP"),
            Q("DALF C1 - Advanced French",                        "DALF-C1",   QualificationType.Language, "French C1 advanced proficiency", "CIEP"),
            Q("DELE B2 - Spanish Language Proficiency",           "DELE-B2",   QualificationType.Language, "Spanish B2 proficiency", "Instituto Cervantes"),
            Q("Goethe-Zertifikat B2 - German",                    "GZ-B2",     QualificationType.Language, "German B2 language certificate", "Goethe-Institut"),
            Q("HSK Level 4 - Mandarin Chinese",                   "HSK4",      QualificationType.Language, "Mandarin Chinese proficiency level 4", "Hanban"),
            Q("JLPT N3 - Japanese Language Proficiency",          "JLPT-N3",   QualificationType.Language, "Japanese language proficiency level N3"),

            // ── Professional Memberships ───────────────────────────────────────
            Q("Fellow of the Institute of Chartered Accountants", "FCA",       QualificationType.Membership, "Fellow membership - chartered accountancy body"),
            Q("Fellow of the Chartered Institute of Management Accountants", "FCMA", QualificationType.Membership, "Fellow membership - CIMA"),
            Q("Member of the British Computer Society",           "MBCS",      QualificationType.Membership, "Professional IT body membership", "BCS"),
            Q("Member of the Institute of Engineering and Technology", "MIET", QualificationType.Membership, "Engineering body membership", "IET"),
            Q("Fellow of the IEEE",                               "FIEEE",     QualificationType.Membership, "Fellow - Institute of Electrical and Electronics Engineers", "IEEE"),
            Q("Member of the Chartered Management Institute",     "MCMI",      QualificationType.Membership, "Management professional body", "CMI"),
            Q("Associate Member of the CIPD",                     "Assoc CIPD",QualificationType.Membership, "Associate membership of CIPD HR body", "CIPD"),
        };
    }
}

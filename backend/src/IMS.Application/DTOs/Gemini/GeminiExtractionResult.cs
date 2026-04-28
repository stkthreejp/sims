namespace IMS.Application.DTOs.Gemini;

public class GeminiExtractionResult
{
    public string? DescriptionOfOperations { get; set; }
    public string? Dba { get; set; }
    public string? EntityType { get; set; }
    public int? YearsInBusiness { get; set; }
    public List<ExtractedDriver> Drivers { get; set; } = [];
    public List<ExtractedVehicle> Vehicles { get; set; } = [];
    public List<ExtractedLocation> Locations { get; set; } = [];
    public List<ExtractedPriorCarrier> PriorCarriers { get; set; } = [];
    public ExtractedSupplemental? Supplemental { get; set; }
    public ExtractedGLCoverages? GLCoverages { get; set; }
    public List<ExtractedGLClassification> GLClassifications { get; set; } = [];
    public ExtractedIMCoverages? IMCoverages { get; set; }
    public List<ExtractedEquipment> Equipment { get; set; } = [];
}

public class ExtractedDriver
{
    public int? DriverNumber { get; set; }
    public string? Name { get; set; }
    public string? DateOfBirth { get; set; }   // YYYY-MM-DD
    public string? LicenseNumber { get; set; }
    public string? LicenseState { get; set; }  // 2-letter
    public string? DateHired { get; set; }     // YYYY-MM-DD
}

public class ExtractedVehicle
{
    public int? UnitNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Vin { get; set; }
    public int? Gvw { get; set; }
    public string? VehicleClass { get; set; }  // Truck | Tractor | Trailer
    public string? GaragingZip { get; set; }
    public string? Radius { get; set; }        // Local | Intermediate
}

public class ExtractedLocation
{
    public int? LocationNumber { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
}

public class ExtractedPriorCarrier
{
    public string? LineOfBusiness { get; set; }
    public string? CarrierName { get; set; }
    public string? PolicyNumber { get; set; }
    public string? ExpirationDate { get; set; } // YYYY-MM-DD
    public decimal? Premium { get; set; }
}

public class ExtractedSupplemental
{
    public List<string> CommoditiesHauled { get; set; } = [];
    public List<string> TerminalLocations { get; set; } = [];
    public List<string> FilingsRequired { get; set; } = [];
    public bool SafetyProgramInPlace { get; set; }
    public bool OwnerOperator { get; set; }
}

public class ExtractedGLCoverages
{
    public decimal? GeneralAggregate { get; set; }
    public decimal? ProductsCompletedOps { get; set; }
    public decimal? EachOccurrence { get; set; }
    public decimal? PersonalAndAdvInjury { get; set; }
    public decimal? DamageToRentedPremises { get; set; }
    public decimal? MedicalExpense { get; set; }
    public decimal? TotalSubcontractorCost { get; set; }
}

public class ExtractedGLClassification
{
    public int? LocationNumber { get; set; }
    public string? ClassCode { get; set; }
    public string? Description { get; set; }
    public string? PremiumBasis { get; set; }
    public decimal? Exposure { get; set; }
}

public class ExtractedIMCoverages
{
    public decimal? ScheduledEquipmentTotalLimit { get; set; }
    public decimal? UnscheduledEquipmentLimit { get; set; }
    public decimal? MaximumValueAnyOneItem { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? CoinsurancePercentage { get; set; }
}

public class ExtractedEquipment
{
    public int? ItemNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }
}

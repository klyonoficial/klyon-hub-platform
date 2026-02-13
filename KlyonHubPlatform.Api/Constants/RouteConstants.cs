namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Nomes dos controllers usados em RedirectToAction e Url.Action.
    /// </summary>
    public static class ControllerNames
    {
        public const string Home = "Home";
        public const string Client = "Client";
        public const string Auth = "Auth";
        public const string Admin = "Admin";
        public const string Coworking = "Coworking";
    }

    /// <summary>
    /// Nomes das actions usados em RedirectToAction e Url.Action.
    /// </summary>
    public static class ActionNames
    {
        // Home
        public const string Index = "Index";
        public const string Marketplace = "Marketplace";
        public const string MarketplaceDetails = "MarketplaceDetails";
        public const string GetAvailabilityForMonth = "GetAvailabilityForMonth";

        // Client
        public const string ChooseLocation = "ChooseLocation";
        public const string RegisterClient = "RegisterClient";
        public const string Profile = "Profile";
        public const string Services = "Services";
        public const string DashboardDetails = "DashboardDetails";
        public const string AvailableAddresses = "AvailableAddresses";
        public const string SelectPaymentPlan = "SelectPaymentPlan";
        public const string CompleteContractAfterLogin = "CompleteContractAfterLogin";
        public const string MyContracts = "MyContracts";
        public const string ContractDetails = "ContractDetails";
        public const string MyPayments = "MyPayments";
        public const string PaymentDetails = "PaymentDetails";
        public const string Declarations = "Declarations";
        public const string BuyCredits = "BuyCredits";
        public const string CheckoutCredits = "CheckoutCredits";
        public const string CreditPurchasePayment = "CreditPurchasePayment";
        public const string BookRoom = "BookRoom";
        public const string MyReservations = "MyReservations";
        public const string FiscalNotes = "FiscalNotes";

        // Auth
        public const string Login = "Login";
        public const string Register = "Register";
        public const string ChangePasswordRequired = "ChangePasswordRequired";

        // Admin
        public const string Contracts = "Contracts";
        public const string Dashboard = "Dashboard";
        public const string Users = "Users";
        public const string EditUser = "EditUser";
        public const string ViewUser = "ViewUser";
        public const string Coworkings = "Coworkings";
        public const string ViewCoworking = "ViewCoworking";
        public const string EditCoworking = "EditCoworking";
        public const string Locations = "Locations";
        public const string EditLocation = "EditLocation";
        public const string ReportCoworkingRevenue = "ReportCoworkingRevenue";
        public const string SystemConfigurations = "SystemConfigurations";
        public const string EditSystemConfiguration = "EditSystemConfiguration";
        public const string Payments = "Payments";
        public const string DatabaseManager = "DatabaseManager";
        public const string ServiceRemovalRequests = "ServiceRemovalRequests";
        public const string SefazConfiguration = "SefazConfiguration";
        public const string ImportedFiscalNotes = "ImportedFiscalNotes";
        public const string CommissionFiscalNotes = "CommissionFiscalNotes";
        public const string Transfers = "Transfers";
        public const string QuickClientRegistration = "QuickClientRegistration";
        public const string QuickClientRegistrationSuccess = "QuickClientRegistrationSuccess";
        public const string InvoiceValidation = "InvoiceValidation";

        // Coworking
        public const string MyAddresses = "MyAddresses";
        public const string CardPrices = "CardPrices";
        public const string ActiveContracts = "ActiveContracts";
        public const string Reservations = "Reservations";
        public const string Commissions = "Commissions";
        public const string Products = "Products";
        public const string AddressRooms = "AddressRooms";
        public const string PendingPayments = "PendingPayments";
        public const string ValidatePayment = "ValidatePayment";
        public const string ServiceFiscalNotes = "ServiceFiscalNotes";
        public const string ListServiceFiscalNotes = "ListServiceFiscalNotes";
        public const string MonthlySettlements = "MonthlySettlements";
    }
}

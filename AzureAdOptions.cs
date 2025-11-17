namespace MipsTestApp.Configuration
{
    public class AzureAdOptions
    {
        public string ClientId { get; set; }        //Set via Environment Variable
        public string AudienceIds { get; set; }     //Set via Environment Variable
        public string Domain { get; set; }
        public string Instance { get; set; }
        public string TenantId { get; set; }        //Set via Environment Variable
        public string ClientSecret { get; set; }    //Set via Environment Variable

    }
}

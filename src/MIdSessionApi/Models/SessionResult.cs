namespace MIdSessionApi.Models
{
    public class SessionResult
    {
        public string SessionId { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public string User { get; set; }
    }
}

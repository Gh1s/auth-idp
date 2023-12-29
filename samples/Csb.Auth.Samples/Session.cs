namespace Csb.Auth.Samples
{
    public record Session
    {
        public string Sub { get; }

        public string Sid { get; }

        public Session(string sub, string sid) => (Sub, Sid) = (sub, sid);
    }
}

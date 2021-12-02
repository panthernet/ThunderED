namespace ThunderED.Thd
{
    public class ThdToken
    {
        public long Id { get; set; }
        public long CharacterId { get; set; }
        public string Token { get; set; }
        public TokenEnum Type { get; set; }
        public long? Roles { get; set; }
        public string Scopes { get; set; }

        public ThdAuthUser User { get; set; }
    }
}

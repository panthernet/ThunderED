namespace ThunderED.Thd
{
    public class ThdGroup
    {
        public long GroupId { get; set; }
        public long CategoryId { get; set; }
        public string GroupName { get; set; }
        public long IconId { get; set; }
        public bool UseBasePrice { get; set; }
        public bool Anchored { get; set; }
        public bool Anchorable { get; set; }
        public bool Fittable { get; set; }
        public bool Published { get; set; }
    }
}

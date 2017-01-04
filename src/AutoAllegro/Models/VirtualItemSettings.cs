namespace AutoAllegro.Models
{
    public class VirtualItemSettings
    {
        public int Id { get; set; }
        public string MessageTemplate { get; set; }
        public string MessageSubject { get; set; }
        public string ReplyTo { get; set; }
        public string DisplayName { get; set; }
    }
}
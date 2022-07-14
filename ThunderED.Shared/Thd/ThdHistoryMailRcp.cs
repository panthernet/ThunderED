namespace ThunderED.Thd;

public class ThdHistoryMailRcp
{
    public long Id { get; set; }
    public long MailId { get; set; }
    public long RecipientId { get; set; }
    public string RecipientType { get; set; }
    public CharacterSnapshot RecipientSnapshot { get; set; }

    public virtual ThdHistoryMail Mail { get; set; }
}
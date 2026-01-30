namespace NinjaDAM.Entity.Entities
{
    public class VerifyEmail
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string Otp { get; set; }
        public DateTime ExpiryTime { get; set; }
        public bool IsVerified { get; set; }
    }
}

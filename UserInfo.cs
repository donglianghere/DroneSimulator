using DroneSimulator;
namespace DroneSimulator
{
    public enum UserType { Student, Teacher, Admin }

    public class UserInfo
    {
        public required string Name { get; set; }
        public required string IdNumber { get; set; }
        public required string Password { get; set; }
        public required UserType Type { get; set; }
    }
}
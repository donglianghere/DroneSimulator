using DroneSimulator;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
namespace DroneSimulator
{
    public static class UserManager
    {
        private static List<UserInfo> users = new();
        private static readonly string FilePath = "users.json";
        public static void Load()
        {
            if (File.Exists(FilePath))
                users = JsonSerializer.Deserialize<List<UserInfo>>(File.ReadAllText(FilePath)) ?? new();
        }

        public static void Save()
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(users));
        }

        public static UserInfo? FindUser(string id, UserType type) =>
            users.FirstOrDefault(u => u.IdNumber == id && u.Type == type);

        public static bool AddUser(UserInfo user)
        {
            if (FindUser(user.IdNumber, user.Type) != null) return false;
            users.Add(user);
            Save();
            return true;
        }

        public static void DeleteUser(string idNumber, UserType type)
        {
            var user = users.FirstOrDefault(u => u.IdNumber == idNumber && u.Type == type);
            if (user != null)
            {
                users.Remove(user);
                Save();
            }
        }

        public static bool ValidateUser(string name, string id, string pwd, UserType type)
        {
            var user = FindUser(id, type);
            return user != null && user.Name == name && user.Password == pwd;
        }

        public static void UpdateUserPassword(string idNumber, UserType type, string newPassword)
        {
            var user = FindUser(idNumber, type);
            if (user != null)
            {
                user.Password = newPassword;
                Save(); // 假设有Save方法持久化
            }
        }

        public static List<UserInfo> GetAllUsers() => users;
    }
}
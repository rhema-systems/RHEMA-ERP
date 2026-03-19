using System;
using Microsoft.AspNetCore.Identity;

namespace PasswordHasherApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var hasher = new PasswordHasher<object>();
            var password = "Admin123!";
            var hash = hasher.HashPassword(null, password);
            Console.WriteLine(hash);
        }
    }
}

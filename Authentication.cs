using System;
using System.Linq;
using System.Security.Cryptography;

namespace TcpClientServerSolution
{
    class Authentication
    {
        public bool Authorize(string username, string password)
        {
            using (var databaseContext = new UserContext())
            {
                if (!databaseContext.Users.Any(user => user.Username == username))
                    return false;

                string savedPassword;
                savedPassword = databaseContext.Users.First(user => user.Username == username).Passwordhash;

                byte[] hashBytes = Convert.FromBase64String(savedPassword);
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000);
                byte[] hash = pbkdf2.GetBytes(20);
                for (int i = 0; i < 20; i++)
                    if (hashBytes[i + 16] != hash[i])
                        return false;

                return true;
            }
        }

        public bool Register(string username, string password)
        {
            using (var databaseContext = new UserContext())
            {
                if (databaseContext.Users.Any(user => user.Username == username))
                    return false;

                byte[] salt, hash;
                new RNGCryptoServiceProvider().GetBytes(salt = new byte[16]);
                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000);
                hash = pbkdf2.GetBytes(20);

                byte[] hashbytes = new byte[36];
                Array.Copy(salt, 0, hashbytes, 0, 16);
                Array.Copy(hash, 0, hashbytes, 16, 20);

                string passwordHash = Convert.ToBase64String(hashbytes);

                databaseContext.Users.Add(new User { Username = username, Passwordhash = passwordHash });
                databaseContext.SaveChanges();

                return true;
            }
        }
    }
}

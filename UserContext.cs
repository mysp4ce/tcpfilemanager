using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;

namespace TcpClientServerSolution
{
    class UserContext : DbContext
    {
        public UserContext() : base("model")
        { }
        public DbSet<User> Users { get; set; }
    }

    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Username { get; set; }
        public string Passwordhash { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SteelProgress.Data.Context;

namespace SteelProgress.Data.Factories
{
    //Esta clase le dice a EFCore como crear AppDbContext, que proveedor usar y donde estará la base de datos de SQLite
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseSqlite("Data Source=steelprogress.db");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}

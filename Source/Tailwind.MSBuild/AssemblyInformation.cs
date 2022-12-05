using System.Reflection;

namespace Tailwind.MSBuild
{
    /// <summary>
    ///     Internal helper class for reading assembly attributes.
    /// </summary>
    internal class Product
    {
        /// <summary>
        ///     Returns a reference to the assembly in which <see cref="Product" /> is declared.
        /// </summary>
        public static Assembly Assembly => typeof(Product).Assembly;

		/// <summary>
		///     Returns product information for the assembly in which <see cref="Product" /> is declared.
		/// </summary>
		public static Product Details => new Product(typeof(Product).Assembly);

        /// <summary>
        ///     Gets the assembly title attribute.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        ///     Gets the assembly file version attribute.
        /// </summary>
        public string Version { get; protected set; }

        /// <summary>
        ///     Gets the assembly description attribute.
        /// </summary>
        public string Description { get; protected set; }

        /// <summary>
        ///     Gets the assembly company attribute.
        /// </summary>
        public string Company { get; protected set; }

        protected Product(Assembly assembly)
        {
            Name = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            Version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
        }
    }
}

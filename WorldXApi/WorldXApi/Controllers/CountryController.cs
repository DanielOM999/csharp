using K4os.Compression.LZ4.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.Intrinsics.X86;
using WorldXApi.Models;
using static Org.BouncyCastle.Asn1.Cmp.Challenge;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WorldXApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CountryController : ControllerBase
    {
        // her er en privat, kun-lesbar variabel av typen IConfiguration, som brukes til å hente konfigurasjonsinnstillinger
        // i en.NET-applikasjon, som for eksempel verdier fra appsettings.json eller enviroment variables.
        private readonly IConfiguration _configuration;

        // constructur-en public CountryController tar imot et IConfiguration-objekt som
        // parameter og tilordner det til den private _configuration-variabelen. Dette gjør at controlleren kan få tilgang
        // til konfigurasjonsinnstillinger i applikasjonen, for eksempel innstillinger fra appsettings.json eller enviroment variables.
        public CountryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // brukes i ASP.NET Core for å markere at en methoden i controlleren skal håndtere
        // HTTP GET-forespørsler. (POST, PUT, og DELETE)
        [HttpGet]

        // Methoden GetCountries() henter en liste med land fra en MySQL-database ved å åpne en tilkobling med en tilkoblings streng
        // hentet fra konfigurasjonen. Den utfører en SQL-spørring for å hente landnavn, leser resultatene
        // med en MySqlDataReader, og legger hvert land til en liste av Country-objekter. Til slutt returneres
        // listen som et JSON-objekt.
        public IActionResult GetCountries()
        {
            var countries = new List<Country>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var query = "SELECT name FROM country";
                using (var command = new MySqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            countries.Add(new Country { Name = reader.GetString("name") });
                        }
                    }
                }
            }

            return Ok(countries);
        }
    }
}
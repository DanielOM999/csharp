using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CryptoPrices.Pages
{
    public class IndexModel : PageModel
    {
        public class CryptoPrice
        {
            // En offentlig propertie kalt Bitcoin av typen decimal. Denne propertien skal lagrer den n�v�rende prisen
            // p� Bitcoin i form av et desimaltall.
            public decimal Bitcoin { get; set; }
            // En offentlig propertie kalt Ethereum av typen decimal. Denne propertien skal lagrer den n�v�rende prisen
            // p� Bitcoin i form av et desimaltall.
            public decimal Ethereum { get; set; }
            // En offentlig propertie kalt bitcoinHistory som er en liste av lister som skal ineholde decimal verdier.
            // Dette brukes til � lagre historikken til Bitcoin-priser de siste 7 dagene. Deretter initialiserer
            // bitcoinHistory som en ny tom liste n�r objektet opprettes.
            public List<List<decimal>> bitcoinHistory { get; set; } = new List<List<decimal>>();
            // En offentlig propertie kalt bitcoinHistoryTime som er en liste av lister som skal ineholde string verdier.
            // Dette brukes til � lagre datoene for n�r prisene er hentet. Deretter initialiserer bitcoinHistoryTime som en ny tom
            // liste n�r objektet opprettes.
            public List<List<string>> bitcoinHistoryTime { get; set; } = new List<List<string>>();
            // En offentlig propertie kalt ethereumHistory som er en liste av lister som skal ineholde decimal verdier.
            // Dette brukes til � lagre historikken til Ethereum-priser de siste 7 dagene. Deretter initialiserer
            // ethereumHistory som en ny tom liste n�r objektet opprettes.
            public List<List<decimal>> ethereumHistory { get; set; } = new List<List<decimal>>();
            // En offentlig propertie kalt ethereumHistoryTime som er en liste av lister som skal ineholde string verdier.
            // Dette brukes til � lagre datoene for n�r prisene er hentet. Deretter initialiserer ethereumHistoryTime som en ny tom
            // liste n�r objektet opprettes.
            public List<List<string>> ethereumHistoryTime { get; set; } = new List<List<string>>();
        }

        // Dette er en offentlig propertie kalt Prices som bruker typen CryptoPrice lagd tidligere
        public CryptoPrice Prices { get; set; } = new CryptoPrice();

        public async Task OnGet()
        {
            // Oppretter en ny instans av HttpClient, som brukes til � sende HTTP-foresp�rsler og svar
            // derreter lages tre variabler med linene til API-ene jeg fetcher
            using var client = new HttpClient();
            string URL = "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum&vs_currencies=nok";
            string L7BURL = "https://api.coingecko.com/api/v3/coins/bitcoin/market_chart?vs_currency=nok&days=7";
            string L7EURL = "https://api.coingecko.com/api/v3/coins/bitcoin/market_chart?vs_currency=nok&days=7";

            try
            {
                // Denne koden sender f�rst en HTTP-foresp�rsel til url-ene som ble satt tidligere so variabler.
                // dette gj�res ved hjelp av metoden FetchWithRetry, som h�ndterer feilh�ndtering og pr�ver p� nytt
                // ved behov. Resultatet av foresp�rselen lagres i variabelene og deserialiseres deretter til en C# lesbar struktur
                // dette ved hjelp av JsonConvert.DeserializeObject.
                var response = await FetchWithRetry(client, URL);
                var jsonData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, decimal>>>(response);

                var bitcoinHistory = await FetchWithRetry(client, L7BURL);
                var bitcoinHistoryData = JsonConvert.DeserializeObject<Dictionary<string, List<List<decimal>>>>(bitcoinHistory);

                var ethereumHistory = await FetchWithRetry(client, L7EURL);
                var ethereumHistoryData = JsonConvert.DeserializeObject<Dictionary<string, List<List<decimal>>>>(ethereumHistory);

                // Denne koden sjekker f�rst om jsonData ikke er null og om den inneholder n�klene "bitcoin" og "ethereum".
                // Hvis s�nn s� tilordnes verdiene for Bitcoin og Ethereum til Prices som Bitcoin. dette gj�res ogs� for Ethereum
                // og tilslutt s� passes det p� at dataen er lagret i norske kroner ellers blir verdien 0.
                if (jsonData != null && jsonData.ContainsKey("bitcoin") && jsonData.ContainsKey("ethereum"))
                        {
                            Prices.Bitcoin = jsonData["bitcoin"].ContainsKey("nok") ? jsonData["bitcoin"]["nok"] : 0;
                            Prices.Ethereum = jsonData["ethereum"].ContainsKey("nok") ? jsonData["ethereum"]["nok"] : 0;

                    Console.WriteLine($"Bitcoin: {Prices.Bitcoin}, Ethereum: {Prices.Ethereum}");
                }


                // Denne koden sjekker om bitcoinHistoryData ikke er null og om den inneholder key-en "prices". Hvis dette er
                // tilfelle, hentes prisdataene fra bitcoinHistoryData og settes til Prices.bitcoinHistory.
                // Deretter grupperes prisene etter dato ved � bruke GroupBy, hvor hver gruppe inneholder alle prisene for en
                // spesifikk dato.Gruppene sorteres etter datoene. To tomme lister, tempPrices og tempTime, opprettes for �
                // lagre de nye prisene og tidene etter at de er bearbeidet. For hver gruppe hentes den nyeste prisdataen ved
                // � sortere gruppen etter tid(entry[0]), og den siste den nyeste dataen tas ut. Tiden konverteres fra
                // Unix - tid til et DateTime - objekt, og formateres deretter til en streng med formatet "yyyy.MM.dd". Den nyeste
                // prisen og den formaterte tiden legges til tempPrices og tempTime midlertidig. Til slutt settes
                // Prices.bitcoinHistory og Prices.bitcoinHistoryTime til de nye listene(tempPrices og tempTime),
                // og disse dataene blir gjort tilgjengelige i ViewData for � kunne brukes i nettsiden.
                if (bitcoinHistoryData != null && bitcoinHistoryData.ContainsKey("prices"))
                {
                    Prices.bitcoinHistory = bitcoinHistoryData["prices"];

                    var groupedByDay = Prices.bitcoinHistory
                        .GroupBy(innerList => DateTimeOffset.FromUnixTimeMilliseconds((long)innerList[0]).Date)
                        .OrderBy(group => group.Key);

                    List<List<decimal>> tempPrices = new List<List<decimal>>();
                    List<List<string>> tempTime = new List<List<string>>();

                    foreach (var group in groupedByDay)
                    {
                        var latestEntry = group.OrderBy(entry => entry[0]).Last();
                        var timeMilliseconds = (long)latestEntry[0];
                        var time = DateTimeOffset.FromUnixTimeMilliseconds(timeMilliseconds).DateTime;
                        var formattedTime = $"{time:yyyy}.{time:MM}.{time:dd}";

                        var price = latestEntry[1];
                        tempPrices.Add(new List<decimal> { price });
                        tempTime.Add(new List<string> { formattedTime });
                    }


                    Prices.bitcoinHistory = tempPrices;
                    Prices.bitcoinHistoryTime = tempTime;

                    ViewData["BitcoinHistory"] = Prices.bitcoinHistory;
                    ViewData["BitcoinHistoryTime"] = Prices.bitcoinHistoryTime;
                }

                // det samme skjer her bare at det er etherium prisene n�
                if (ethereumHistoryData != null && ethereumHistoryData.ContainsKey("prices"))
                {
                    Prices.ethereumHistory = ethereumHistoryData["prices"];

                    var groupedByDay = Prices.ethereumHistory
                        .GroupBy(innerList => DateTimeOffset.FromUnixTimeMilliseconds((long)innerList[0]).Date)
                        .OrderBy(group => group.Key);

                    List<List<decimal>> tempPrices = new List<List<decimal>>();
                    List<List<string>> tempTime = new List<List<string>>();

                    foreach (var group in groupedByDay)
                    {
                        var latestEntry = group.OrderBy(entry => entry[0]).Last();
                        var timeMilliseconds = (long)latestEntry[0];
                        var time = DateTimeOffset.FromUnixTimeMilliseconds(timeMilliseconds).DateTime;
                        var formattedTime = $"{time:yyyy}.{time:MM}.{time:dd}";

                        var price = latestEntry[1];
                        tempPrices.Add(new List<decimal> { price });
                        tempTime.Add(new List<string> { formattedTime });
                    }


                    Prices.ethereumHistory = tempPrices;
                    Prices.ethereumHistoryTime = tempTime;

                    ViewData["EthereumHistory"] = Prices.ethereumHistory;
                    ViewData["EthereumHistoryTime"] = Prices.ethereumHistoryTime;
                }
            }
            // Denne koden fanger opp errore og setter prisene p� Bitcoin og Ethereum til 0 hvis noe g�r galt.
            // Feilmeldingen skrives i konsollen ogs�.
            catch (Exception err)
            {
                Prices = new CryptoPrice { Bitcoin = 0, Ethereum = 0 };
                Console.WriteLine("Error fetching data: " + err.Message);
            }
        }

        // Denne methoden pr�ver � hente data fra en API med maks tre fors�k. Hvis svaret er vellykket(statuskode 200),
        // returneres dataene som en streng. Hvis API-et gir en 429 (rate-limiting) feilmelding, vil metoden vente f�r den
        // pr�ver p� nytt, og fors�kene �kes med en �kende forsinkelse p� 1 minutt. Hvis det ikke er vellykket etter flere
        // fors�k, settes en feil.
        private async Task<string> FetchWithRetry(HttpClient client, string url, int maxRetries = 3)
        {
            int retries = 0;
            while (retries < maxRetries)
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if ((int)response.StatusCode == 429)
                {
                    retries++;
                    int delay = 60000 * retries;
                    Console.WriteLine($"Rate limited. Retrying after {delay}ms...");
                    await Task.Delay(delay);
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            throw new Exception("Max retry attempts exceeded.");
        }
    }
}

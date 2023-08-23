using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        Logger.writeLine($"Welcome to the KAHA Travel Bot");
        Logger.writeLine($"Fetching all countries from https://restcountries.com");
        var countries = await GetAllCountries();
        if (!countries.Any()) 
        {
            Logger.writeLine($"Error fetching countries!");
            return;
        }
        Logger.writeLine($"Choosing random country from the southern hemisphere...");
        var randomCountry = RandomCountryInSouthernHemisphere(countries);
        Logger.writeLine($"Selected Random Country: {randomCountry.Name}");
        await randomCountry.sunTimes();
        randomCountry.summary();
    }

    public static async Task<List<Country>> GetAllCountries()
    {
        var apiUrl = "https://restcountries.com/v3.1/all";
        using (var httpClient = new HttpClient())
        {
            var countries = new List<Country>();
            try
            {
                var response = await httpClient.GetStringAsync(apiUrl);
                var parsedResponse = JArray.Parse(response);
                foreach (var countryData in parsedResponse)
                {
                    try
                    {
                        var country = ParseCountryData(countryData);
                        countries.Add(country);
                    }
                    catch
                    {
                        var name = countryData["name"]["common"].ToString();
                        Console.WriteLine($"Error parsing data for: {name}");
                        continue;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error handling request due to: {ex.Message}");
            }
            return countries;
        }
    }


    static Country RandomCountryInSouthernHemisphere(List<Country> countries)
    {
        var countriesInSouthernHemisphere = countries.Where(x => x.Latitude < 0);
        var random = new Random();
        var randomIndex = random.Next(0, countriesInSouthernHemisphere.Count());
        return countriesInSouthernHemisphere.ElementAt(randomIndex);
    }

    static async Task<(string rise, string set)> GetSunriseSunsetTimes(Country country)
    {
        string queryUrl = buildQueryUrl(country);
        using (var httpClient = new HttpClient())
        {
            try
            {
                var response = await httpClient.GetStringAsync(queryUrl);
                var results = JObject.Parse(response)["results"];

                return (results["sunrise"].ToString(), results["sunset"].ToString());
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error requesting SunRise and Sunset times: {ex.Message}");
                throw;
            }
        }

    }

    private static Country ParseCountryData(JToken countryData)
    {
        var name = countryData["name"]?["common"]?.ToString();
        var capital = countryData["capital"]?[0]?.ToString() ?? "Unknown";
        var languages = countryData["languages"]?.ToObject<Dictionary<string, string>>().Count?? 0;
        var drivingSide = countryData["car"]?["side"]?.ToString();
        float latitude = 0.0f;
        float longitude = 0.0f; 

        if (float.TryParse(countryData["capitalInfo"]?["latlng"]?[0]?.ToString(), out var lat))
        {
            latitude = lat;
        }
        if (float.TryParse(countryData["capitalInfo"]?["latlng"]?[1]?.ToString(), out var lon))
        {
            longitude = lon;
        }

        return new Country
        {
            Name = name,
            Capital = capital,
            Latitude = latitude,
            Longitude = longitude,
            Languages = languages,
            drivingSide = drivingSide == "left"? DrivingSide.left : DrivingSide.right
        };
    }

    private static string buildQueryUrl(Country country)
    {
        string baseUrl = "https://api.sunrise-sunset.org/json";
        DateTime tomorrow = DateTime.Today.AddDays(1);
        string tomorrowFormatted = tomorrow.ToString("yyyy-MM-dd");
        Dictionary<string, string> queryParams = new Dictionary<string, string>
        {
            { "lat", country.Latitude.ToString() },
            { "lng", country.Longitude.ToString() },
            { "date", tomorrowFormatted}
        };
        var queryString = new StringBuilder();
        queryString.Append("?");
        foreach (var param in queryParams)
        {
            queryString.Append(Uri.EscapeDataString(param.Key));
            queryString.Append("=");
            queryString.Append(Uri.EscapeDataString(param.Value));
            queryString.Append("&");
        }

        string fullUrl = baseUrl + queryString.ToString().TrimEnd('&');

        return fullUrl;
    }

    public class Country
    {
        public string Name { get; set; }
        public string Capital { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public long Languages { get; set; }
        public DrivingSide drivingSide { get; set; }

        public async Task sunTimes ()
        {
            var sunTimes = await Program.GetSunriseSunsetTimes(this);
            Logger.writeGreenLine($"For Selected Country, the Sun rises tomorrow at {sunTimes.rise} and Sets at {sunTimes.set}");
        }

        public void summary ()
        {
            Logger.writeGreenLine($"{this.Name}, Has a total of {Languages} Languages and Here, Driving is on the {drivingSide} Side.");
            Logger.writeGreenLine($"Little Fact: The distance Between {this.Name}'s Capital and Kaha's Office is Approximately {calcDistanceFromKaha()} KM.");
        }

        public double calcDistanceFromKaha() // function makes use of the Haversine formula to calculate distance based on coordinates.
        {
            (float lat, float lng) = (-33.9759679f, 18.4566283f); // Coordinates of Kaha gotten from Maps link.
            const float EarthRadiusKm = 6371f;

            float lattitudeDiff = DegreeToRadians(lat - this.Latitude);
            float longitudeDiff = DegreeToRadians(lng - this.Longitude);

            double a = Math.Sin(lattitudeDiff / 2) * Math.Sin(lattitudeDiff / 2) +
                    Math.Cos(DegreeToRadians(lat)) * Math.Cos(DegreeToRadians(this.Latitude)) *
                    Math.Sin(longitudeDiff / 2) * Math.Sin(longitudeDiff / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double distance = EarthRadiusKm * c;
            return Math.Round(distance);
        }

        static float DegreeToRadians(float degrees)
        {
            return degrees * (float)Math.PI / 180;
        }
    }

    class Logger
    {
        public static void writeLine(string text)
        {
            TypeWriterEffect(text);
        }

        public static void writeGreenLine(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            TypeWriterEffect(text);
            Console.ResetColor();
        }

        private static void TypeWriterEffect(string text)
        {
            foreach (char c in text)
            {
                Console.Write(c);
                Thread.Sleep(20); // we delay for 20 secs to give some typeWritter Effect.
            }
            Console.WriteLine();
        }
    }

    public enum DrivingSide 
    {
        left,
        right
    }
}

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Freud.Modules.Search.Common
{
    public class WeatherData
    {
        public List<Weather> Weather { get; set; }
        public Coord Coord { get; set; }
        public Main Main { get; set; }
        public int Visibility { get; set; }
        public Wind Wind { get; set; }
        public Clouds Clouds { get; set; }
        public int Dt { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int Cod { get; set; }
        public Sys Sys { get; set; }
    }

    public class PartialWeatherData
    {
        public List<Weather> Weather { get; set; }
        public Main Main { get; set; }
        public int Visibility { get; set; }
        public Wind Wind { get; set; }
        public Clouds Clouds { get; set; }
        public int Dt { get; set; }
        public int Id { get; set; }
        public int Cod { get; set; }
        public Sys Sys { get; set; }
    }

    public class Forecast
    {
        public City City { get; set; }

        [JsonProperty("list")]
        public List<PartialWeatherData> WeatherDataList { get; set; }
    }

    public class Coord
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class City
    {
        public Coord Coord { get; set; }
        public string Country { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Weather
    {
        public string Description { get; set; }
        public string Icon { get; set; }
        public int Id { get; set; }
        public string Main { get; set; }
    }

    public class Main
    {
        public double Temperature { get; set; }
        public float Pressure { get; set; }
        public float Humidity { get; set; }

        [JsonProperty("temp_min")]
        public double TemperatureMin { get; set; }

        [JsonProperty("temp_max")]
        public double TemperatureMax { get; set; }
    }

    public class Wind
    {
        public double Speed { get; set; }
        public double Degrees { get; set; }
    }

    public class Clouds
    {
        public int All { get; set; }
    }

    public class Sys
    {
        public int Type { get; set; }
        public int Id { get; set; }
        public double Message { get; set; }
        public string Country { get; set; }
        public double Sunrise { get; set; }
        public double Sunset { get; set; }
    }
}

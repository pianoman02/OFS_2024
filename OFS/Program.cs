using System;
using System.Globalization;
using System.IO;

namespace OFS
{
    internal class Program
    {
        static void ReadFile(string filename, List<double> storage)
        {
            var reader = new StreamReader(@"..\..\..\..\Data\" + filename);

            CultureInfo culture = new CultureInfo("nl");
            reader.ReadLine(); // discard first line
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(';');
                Console.WriteLine(values[1]);
                storage.Add(double.Parse(values[1], culture));
            }
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // read data from files
            List<double> ArrivalDistribution = new List<double>();
            List<double> ChargingVolumeDistribution = new List<double>();
            List<double> ConnectionTimeDistribution = new List<double>();
            ReadFile("arrival_hours.csv", ArrivalDistribution);
            ReadFile("arrival_hours.csv", ChargingVolumeDistribution);
            ReadFile("arrival_hours.csv", ConnectionTimeDistribution);


            // start and run a priority queue
            PriorityQueue<Event, int> EventQueue = new PriorityQueue<Event, int>();

        }
    }
    interface Event
    {
    public void CallEvent();
    }
    public class EndSimulation: Event
    {
        public void CallEvent()
        {
            // nothing
        }
    }
}
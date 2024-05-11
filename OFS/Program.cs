using System.Globalization;
using MathNet.Numerics.Distributions;

namespace OFS
{
    //delegate void Event();
    internal class Program
    {
        public static PriorityQueue<Event, double> eventQueue = new();
        public static State state = new();

        static void ReadFile(string filename, List<double> storage)
        {
            var reader = new StreamReader(@"..\..\..\..\Data\" + filename);

            CultureInfo culture = new CultureInfo("nl");
            reader.ReadLine(); // discard first line
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(';');
                storage.Add(double.Parse(values[1], culture));
            }
        }
        static void ReadCumProb(string filename, List<double> output)
        {
            List<double> prob_distribution = new List<double>();
            ReadFile(filename, prob_distribution);
            double cumProb = 0;
            foreach (double p in prob_distribution)
            {
                cumProb += p;
                output.Add(cumProb);
            }
        }
        static void Main(string[] args)
        {
            Console.Write("Reading input...");

            // read data from files
            ReadFile("arrival_hours.csv", Data.ArrivalDistribution);
            ReadCumProb("charging_volume.csv", Data.ChargingVolumeCumulativeProbabilty);
            ReadCumProb("connection_time.csv", Data.ConnectionTimeCumulativeProbabilty);
            // TODO: Read solar panel data, dependent on summer or winter.
            for (int i = 0; i < 24; i++)
                Data.SolarPanelAverages.Add(0.1);

         
            Console.WriteLine("Done");
            // Initialise lists in History
            for (int i=0; i<10; i++)
            {
                History.CableChangeLoads[i] = new List<double>();
                History.CableChangeTimes[i] = new List<double>();
            }

            // start and run a priority queue
            Console.WriteLine("Starting simulation");
            
            eventQueue.Enqueue(new EndSimulation(100), 100);
            eventQueue.Enqueue(new CarArrives(0), 0);
            eventQueue.Enqueue(new SolarPanelsChange(state.stations[5], 0), 0);
            eventQueue.Enqueue(new SolarPanelsChange(state.stations[6], 0), 0);
            //State.EventQueue.Enqueue(new SolarPanelsChange(0, 0), 0);
            //State.EventQueue.Enqueue(new SolarPanelsChange(0, 1), 0);

            while (eventQueue.Count > 0)
            {
                Event e = eventQueue.Dequeue();
                e.CallEvent();
            }
            Console.WriteLine("Simulation finished");

        }
    }
    static public class Data
    {
        static public List<double> ArrivalDistribution = new List<double>();
        static public List<double> ChargingVolumeCumulativeProbabilty = new List<double>();
        static public List<double> ConnectionTimeCumulativeProbabilty = new List<double>();
        static public List<double> SolarPanelAverages = new List<double>();
        static public int[] ParkingCapacities = { 60, 80, 60, 70, 60, 60, 50 }; // zero based, so all the spot move one number
        static public double[] ParkingDistributionCumulative = { 0.15, 0.3, 0.45, 0.65, 0.8, 0.9, 1};
        static public double[] CableCapacities = { 1000, 200, 200, 200, 200, 200, 200, 200, 200, 200 };
    }
    public class State
    {
        public Station[] stations = new Station[7];
        public Cable[] cables = new Cable[10];

        public State()
        {
            cables[0] = new Cable(1000);
            cables[1] = new Cable(200, cables[0]);
            cables[2] = new Cable(200, cables[1]);
            cables[3] = new Cable(200, cables[1]);
            cables[4] = new Cable(200, cables[1]);
            cables[5] = new Cable(200, cables[0]);
            cables[6] = new Cable(200, cables[5]);
            cables[7] = new Cable(200, cables[5]);
            cables[8] = new Cable(200, cables[7]);
            cables[9] = new Cable(200, cables[7]);

            stations[0] = new Station(cables[2], 60);
            stations[1] = new Station(cables[3], 80);
            stations[2] = new Station(cables[4], 60);
            stations[3] = new Station(cables[5], 70);
            stations[4] = new Station(cables[8], 60);
            stations[5] = new Station(cables[9], 60);
            stations[6] = new Station(cables[6], 50);
        }
    }
    static public class History
    {
        static public List<double>[] CableChangeTimes = new List<double>[10];
        static public List<double>[] CableChangeLoads = new List<double>[10];
        static public int CarsRejected = 0;
    }
    static public class Random
    {
        // Picks a number accoring to the cdf
        static public int SampleCDF(IList<double> cdf)
        {
            double r = ContinuousUniform.Sample(0, 1);
            for (int i = 0; i < cdf.Count(); i++)
            {
                if (r < cdf[i])
                {   
                    return i;
                }
            }
            throw new Exception("cdf didn't end with a 1");
        }
        // Picks an interval according to the cdf and takes an arbitrary number in this interval
        static public double SampleContCDF(IList<double> cdf)
        {
            int val = SampleCDF(cdf);
            return ContinuousUniform.Sample(val, val + 1);
        }
        /// <summary>
        /// Takes an exponential amount of time, but corrects for the fact that that might cross the hour tickmark.
        /// </summary>
        /// <param name="pdf"></param>
        /// <param name="currenttime"></param>
        /// <returns></returns>
        static public double PoissonSample(IList<double> pdf, double currenttime)
        {
            // TODO: This method should be checked statistically.
            int hour = ((int)Math.Floor(currenttime));
            bool higher = true;
            double deltaTime =0;
            while (higher)
            {
                deltaTime = Exponential.Sample(Data.ArrivalDistribution[hour%24] * 750);
                if (currenttime + deltaTime < hour + 1)
                    higher = false;
                else
                {
                    hour++;
                    currenttime = hour;
                }
            }
            return currenttime + deltaTime;
        }
    }
}
//to change value of SOLAROUTPUT -> look in Project,  OFS properties, Build, General, Conditional compilation symbols

using System.Globalization;
using MathNet.Numerics.Distributions;


namespace OFS
{
    public enum Strategy
    {
        ON_ARRIVAL,
        PRICE_DRIVEN,
        FCFS,
        ELFS
    }
    internal class Program
    {
        public static Simulation simulation = new(0, false, []);
        public const int CHARGE_SPEED = 6;

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
        static void ReadTwoColumnFile(string filename, List<double> output1, List<double> output2)
        {
            var reader = new StreamReader(@"..\..\..\..\Data\" + filename);

            CultureInfo culture = new CultureInfo("nl");
            reader.ReadLine(); // discard first line
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(';');
                output1.Add(double.Parse(values[1], culture));
                output2.Add(double.Parse(values[2], culture));
            }

        }
        static string filename(Strategy strat, bool summer,int solar)
        {
            string name = "";
            name += strat.ToString();
            name += "_";
            name += summer ? "summer" : "winter";
            name += "_";
            name += "solar" + solar.ToString();
            name += ".txt";
            return name;

        }
        static void Main(string[] args)
        {

            Console.Write("Reading input...");

            // read data from files
            ReadFile("arrival_hours.csv", Data.ArrivalDistribution);
            ReadCumProb("charging_volume.csv", Data.ChargingVolumeCumulativeProbabilty);
            ReadCumProb("connection_time.csv", Data.ConnectionTimeCumulativeProbabilty);
            ReadTwoColumnFile("solar.csv", Data.SolarPanelAveragesWinter, Data.SolarPanelAveragesSummer);

            Console.WriteLine("Done");
            // start and run a priority queue

            List<int>[] solarOptions = [[], [5, 6], [0, 1, 5, 6]];

            for (Strategy strat = Strategy.ON_ARRIVAL; strat <= Strategy.ELFS; strat++) {
                foreach (bool summer in new List<bool>{true, false}) {
                    for(int solar = 0; solar<3; solar++) {
                        Console.WriteLine("Starting simulation " + filename(strat,summer,solar));
                        Console.Write("...");
                        simulation = new Simulation(strat, summer, solarOptions[solar]);
                        History result = simulation.RunSimulation();
                        result.OutputResults(filename(strat,summer,solar));
                        Console.WriteLine("     finished");
                    }
                }
            }
        }
    }

    public class Simulation
    {
        public Strategy strategy;
        private static PriorityQueue<Event, double> eventQueue = new();
        public State state = new();
        public History history;
        public List<int> solarStations;
        public bool summer;
        public Simulation(Strategy strategy, bool summer, List<int> solarStations)
        {
            this.solarStations = solarStations;
            this.strategy = strategy;
            this.summer = summer;
            history = new History(state.cables);
            eventQueue.Enqueue(new EndSimulation(100), 100);
            eventQueue.Enqueue(new CarArrives(0), 0);
            eventQueue.Enqueue(new SolarPanelsChange(0), 0);
        }

        public History RunSimulation()
        {
            while (eventQueue.Count > 0)
            {
                Event e = eventQueue.Dequeue();
                e.CallEvent();
            }
            return history;
        }

        public void PlanEvent(Event e)
        {
            eventQueue.Enqueue(e, e.eventTime);
        }

        public void EndSimulation()
        {
            eventQueue.Clear();
        }

        public void RejectCar()
        {
            history.RejectCar();
        }

        internal void Wait(Car car)
        {
            // This priority queue has O(n) time, it is possible in O(log n) time, but it seems fine for now
            for (int i = 0; i < state.waiting.Count; i++) {
                if (car.prio < state.waiting[i].prio) {
                    state.waiting.Insert(i, car);
                    return;
                }
            }
            state.waiting.Add(car);
        }

        // Onderstaande was even puzzelen, maar het klopt wel!
        internal void TryPlanNextCar(double time)
        {
            if (strategy >= Strategy.FCFS) {
                List<Car> cars = [];
                foreach (Car car in state.waiting) {
                    if (car.CanCharge()) {
                        cars.Add(car);
                        car.station.cable.ChangeVirtualCableFlow(Program.CHARGE_SPEED);
                        Program.simulation.PlanEvent(new StartsCharging(car, time));
                    }
                }
                foreach (Car car in cars)
                {
                    state.waiting.Remove(car);
                }
                Cable.RestoreLoads();
            }
            // In the case of one of the Price_driven or ON_ARRIVAL, no new car needs to be scheduled
        }
    }
    static public class Data
    {
        static public List<double> ArrivalDistribution = new List<double>();
        static public List<double> ChargingVolumeCumulativeProbabilty = new List<double>();
        static public List<double> ConnectionTimeCumulativeProbabilty = new List<double>();
        static public List<double> SolarPanelAveragesSummer = new List<double>();
        static public List<double> SolarPanelAveragesWinter = new List<double>();
        static public int[] ParkingCapacities = { 60, 80, 60, 70, 60, 60, 50 }; // zero based, so all the spot move one number
        static public double[] ParkingDistributionCumulative = { 0.15, 0.3, 0.45, 0.65, 0.8, 0.9, 1};
        static public double[] CableCapacities = { 1000, 200, 200, 200, 200, 200, 200, 200, 200, 200 };
    }
    public class State
    {
        public List<Car> waiting = new();
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

    public class History(Cable[] cables)
    {
        public Cable[] cables = cables;
#if SOLAROUTPUT
        public List<double> solaroutput = new List<double>();
#endif
        private int CarsRejected = 0;

        public void RejectCar()
        {
            CarsRejected++;
        }

        public void OutputResults(string filename)
        {
            var writer = new StreamWriter(@"..\..\..\..\Output\" + filename);
            writer.WriteLine(CarsRejected);
            foreach (Cable c in cables)
            {
                writer.WriteLine(c.changeLoads.Count);
                for (int i=0; i<c.changeLoads.Count; i++)
                {
                    writer.Write(c.changeTimes[i]);
                    writer.Write(";");
                    writer.WriteLine(c.changeLoads[i]);                
                }
            }
            writer.Close();
#if SOLAROUTPUT
            writer = new StreamWriter(@"..\..\..\..\Output\solar_" + filename);
            foreach (double d in solaroutput)
            {
                writer.WriteLine(d);
            }
            writer.Close();
#endif
        }
    }

    public static class RandomDists
    {
        // Picks a number accoring to the cdf
        public static Random rng = new Random(42);
        public static int SampleCDF(IList<double> cdf)
        {
            double r = ContinuousUniform.Sample(rng,0, 1);
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
        public static double SampleContCDF(IList<double> cdf)
        {
            int val = SampleCDF(cdf);
            return ContinuousUniform.Sample(rng,val, val + 1);
        }
        /// <summary>
        /// Takes an exponential amount of time, but corrects for the fact that that might cross the hour tickmark.
        /// </summary>
        /// <param name="pdf"></param>
        /// <param name="currenttime"></param>
        /// <returns></returns>
        public static double PoissonSample(IList<double> pdf, double currenttime)
        {
            // TODO: This method should be checked statistically.
            int hour = ((int)Math.Floor(currenttime));
            bool higher = true;
            double deltaTime =0;
            while (higher)
            {
                deltaTime = Exponential.Sample(rng,Data.ArrivalDistribution[hour%24] * 750);
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
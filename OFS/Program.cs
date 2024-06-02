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
        public static Simulation simulation;
        public const int CHARGE_SPEED = 6;
        public const int SIMULATION_TIME = 2424;
        public const int WARMUP_TIME = 24;
        const string vehicleheader = "\\begin{tabular}{|l|l|l|l|l|}\r\n\\hline\r\nStrategy     & Scenario        & \\begin{tabular}[c]{@{}l@{}}Percentage of\\\\ vehicles\\\\ with delay\\end{tabular} & Average delay & \\begin{tabular}[c]{@{}l@{}}Percentage of\\\\ vehicles\\\\ not served\\end{tabular} \\\\";
        const string cableheader = "\\begin{tabular}{|l|l|l|l|l|l|}\r\n\\hline\r\nStrategy     & Scenario        & \\begin{tabular}[c]{@{}l@{}}Cable 1\\\\ overload\\end{tabular} & \\begin{tabular}[c]{@{}l@{}}Cable 1\\\\ blackout\\end{tabular} & \\begin{tabular}[c]{@{}l@{}}Cable 5\\\\ overload\\end{tabular} & \\begin{tabular}[c]{@{}l@{}}Cable 5\\\\ blackout\\end{tabular} \\\\";
        public const string vehiclepath = @"..\..\..\..\Output\AVehicleTable.txt";
        public const string cablepath = @"..\..\..\..\Output\ACableTable.txt";
        public const int RUNS_PER_SCENARIO = 100;
        public static List<int> carsPerDay = [];

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

        static string simuationname(Strategy strat, bool summer, int solar)
        {
            string name = "";
            name += strat.ToString();
            name += "_";
            name += summer ? "summer" : "winter";
            name += "_";
            name += "solar" + solar.ToString();
            return name;
        }
        static string filename(Strategy strat, bool summer, int solar, int n)
        {
            string name = simuationname(strat, summer, solar);
            name += "_" + n.ToString();
            name += ".txt";
            return name;
        }

        public static void LogArrival(double time)
        {
            if (time > WARMUP_TIME) {
                int day = (int)Math.Floor((time - WARMUP_TIME) / 24);
                simulation.history.dailyVehicles[day] ++;
            }
        }
        static string tablestring(bool summer, int solar)
        {
            string sw = summer ? "S" : "W";
            switch (solar)
            {
                case 0:
                    return "No solar";
                case 1:
                    return "Solar 6,7 " + sw;
                case 2:
                    return "Solar 1,2,6,7 " + sw;
                default:
                    throw new Exception("Only wto solar strategies");
            }
        }
        static string tablestrategy(Strategy strategy)
        {
            switch (strategy)
            {
                case Strategy.ON_ARRIVAL:
                    return "Base";
                case Strategy.PRICE_DRIVEN:
                    return "Price driven";
                default:
                    return strategy.ToString();
            }
        }

        static string twod(double d)
        {
            if (d == 0)
                return "0";
            else
                return d.ToString("F");
        }

        static void Main(string[] args)
        {
            File.WriteAllText(vehiclepath, vehicleheader);
            File.WriteAllText(cablepath, cableheader);

            Console.Write("Reading input...");

            // read data from files
            ReadFile("arrival_hours.csv", Data.ArrivalDistribution);
            ReadCumProb("charging_volume.csv", Data.ChargingVolumeCumulativeProbabilty);
            ReadCumProb("connection_time.csv", Data.ConnectionTimeCumulativeProbabilty);
            ReadTwoColumnFile("solar.csv", Data.SolarPanelAveragesWinter, Data.SolarPanelAveragesSummer);

            // Calculate average charging volume:
            double sum = 0;
            double previous = 0;
            for (int i=0; i<Data.ChargingVolumeCumulativeProbabilty.Count; i++)
            {
                double cumprob = Data.ChargingVolumeCumulativeProbabilty[i];
                sum += (i + 0.5) * (cumprob - previous);
                previous = cumprob;
            }
            Console.WriteLine("Average charging volume is" + sum.ToString());
            Console.WriteLine("Done");
            // start and run a priority queue

            List<int>[] solarOptions = [[], [5, 6], [0, 1, 5, 6]];

            for (Strategy strat = Strategy.ON_ARRIVAL; strat <= Strategy.ELFS; strat++) {
                File.AppendAllText(vehiclepath, "\\hline\r\n" + tablestrategy(strat));
                File.AppendAllText(cablepath, "\\hline\r\n" + tablestrategy(strat));
                for (int solar = 0; solar < 3; solar++)
                {
                    List<bool> seasons = new List<bool> { false };
                    if (solar > 0)
                    {
                        seasons.Add(true);
                    }
                    foreach (bool summer in seasons)
                    {
                        Console.WriteLine("Starting simulation " + simuationname(strat,summer,solar));
                        // latex writing
                        File.AppendAllText(cablepath, "&" + tablestring(summer,solar));
                        File.AppendAllText(vehiclepath, "&" + tablestring(summer, solar));
                        Dictionary<string, double> averageData = new Dictionary<string, double> {
                                {"percdelay", 0},
                                {"avgdelay", 0},
                                {"percnotserved", 0},
                                {"cable1overload", 0},
                                {"cable1blackout", 0},
                                {"cable5overload", 0},
                                {"cable5blackout", 0},
                                };

                        for (int n = 0; n < RUNS_PER_SCENARIO; n++) {
                            string progressBar = "|" + new string ('█', n) + new string (' ', RUNS_PER_SCENARIO - n) + "|\r";
                            Console.Write(progressBar);
                            simulation = new Simulation(strat, summer, solarOptions[solar]);
                            History result = simulation.RunSimulation();
                            Dictionary<string,double> data = result.OutputResults(filename(strat,summer,solar, n));
                            foreach (string key in averageData.Keys) {
                                averageData[key] += data[key] / RUNS_PER_SCENARIO;
                            }
                        }
                        File.AppendAllText(vehiclepath, "&" + twod(averageData["percdelay"] * 100) + "\\%");
                        File.AppendAllText(vehiclepath, "&" + twod(averageData["avgdelay"]) + "h");
                        File.AppendAllText(vehiclepath, "&" + twod(averageData["percnotserved"] * 100) + "\\%");
                        File.AppendAllText(vehiclepath, "\\\\ \\cline{2-5} \r\n");

                        File.AppendAllText(cablepath, "&" + twod(averageData["cable1overload"]*100)+"\\%");
                        File.AppendAllText(cablepath, "&" + twod(averageData["cable1blackout"] * 100) + "\\%");
                        File.AppendAllText(cablepath, "&" + twod(averageData["cable5overload"]*100)+"\\%");
                        File.AppendAllText(cablepath, "&" + twod(averageData["cable5blackout"] * 100) + "\\%");
                        File.AppendAllText(cablepath, "\\\\ \\cline{2-6} \r\n");
                        Console.WriteLine("finished!" + new string (' ', RUNS_PER_SCENARIO + 2));
                    }
                }
            }
            File.AppendAllText(vehiclepath, " \\hline \r\n \\end{tabular}");
            File.AppendAllText(cablepath, " \\hline \r\n \\end{tabular}");
            double average = carsPerDay.Average();
            double sumOfSquaresOfDifferences = carsPerDay.Select(val => (val - average) * (val - average)).Sum();
            double sd = Math.Sqrt(sumOfSquaresOfDifferences / carsPerDay.Count);
            Console.WriteLine("Vehicles per day: {0} ± {1}\n", average, sd);
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
            PlanEvent(new EndSimulation(Program.SIMULATION_TIME));
            PlanEvent(new StartTrackingData(Program.WARMUP_TIME));
            PlanEvent(new CarArrives(0));
            PlanEvent(new SolarPanelsChange(0));
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

        public void LogRejection()
        {
            history.RejectCar();
        }

        public void LogDelay(double time)
        {
            history.LogDelay(time);
        }

        public void LogSolarOutput(double output)
        {
            history.solaroutput.Add(output);
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

        internal void StartTrackingData()
        {
            history.Reset();
        }
    }
    static public class Data
    {
        static public List<double> ArrivalDistribution = new List<double>();
        static public List<double> ChargingVolumeCumulativeProbabilty = new List<double>();
        static public List<double> ConnectionTimeCumulativeProbabilty = new List<double>();
        static public List<double> SolarPanelAveragesSummer = new List<double>();
        static public List<double> SolarPanelAveragesWinter = new List<double>();
        static public double[] ParkingDistributionCumulative = { 0.15, 0.3, 0.45, 0.65, 0.8, 0.9, 1};
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
        public int[] dailyVehicles = new int[(Program.SIMULATION_TIME - Program.WARMUP_TIME) / 24];
        public Cable[] cables = cables;
#if SOLAROUTPUT
        public List<double> solaroutput = [];
#endif
        private int carsRejected = 0;

        private List<double> delays = [];

        public void Reset()
        {
            solaroutput = [];
            carsRejected = 0;
            delays = [];
        }

        public void RejectCar()
        {
            carsRejected++;
        }

        public void LogDelay(double delay)
        {
            delays.Add(delay);
        }

        public Dictionary<string,double> OutputResults(string filename)
        {
            Dictionary<string,double> output = []; // output for latex file
            int carsServed = delays.Count;
            int carsDelayed = 0;
            double totalDelay = 0;
            foreach (double delay in delays) {
                totalDelay += delay;
                if (delay > 0) {
                    carsDelayed++;
                }
            }
            double percdelay = (double)carsDelayed / carsServed;
            double avgdelay = totalDelay / carsServed;
            double percnotserved = (double)carsRejected / (carsRejected + carsServed);
            // and now also in latex:
            output["percdelay"] = percdelay;
            output["avgdelay"] = avgdelay;
            output["percnotserved"] = percnotserved;

            Program.carsPerDay.AddRange(dailyVehicles);

            foreach (int i in new List<int>{1,5})
            {
                Cable c = cables[i];
                double blackoutTime = 0;
                double overloadTime = 0;
                double lastTimeStamp = 0;
                bool overload = false;
                bool blackout = false;
                for (int j=0; j<c.changeLoads.Count; j++)
                {
                    double time = c.changeTimes[j];
                    if (blackout)
                    {
                        blackoutTime += time - lastTimeStamp;
                    } else if (overload) {
                        overloadTime += time - lastTimeStamp;
                    }
                    overload = c.changeLoads[j] > c.capacity;
                    blackout = c.changeLoads[j] > c.capacity * 1.1;
                    lastTimeStamp = time;
                }
                double percoverload = overloadTime / (Program.SIMULATION_TIME - Program.WARMUP_TIME);
                double percblackout = blackoutTime / (Program.SIMULATION_TIME - Program.WARMUP_TIME);
                // latex table
                output["cable" + i.ToString() + "overload"] = percoverload;
                output["cable" + i.ToString() + "blackout"] = percblackout;
                // rest
            }
            StreamWriter writer = new StreamWriter(@"..\..\..\..\Output\" + filename);
            // Now some unreadable code for the python script
            writer.WriteLine(carsRejected);
            foreach (Cable c in cables)
            {
                writer.WriteLine(c.changeLoads.Count);
                for (int i = 0; i < c.changeLoads.Count; i++)
                {
                    writer.Write(c.changeTimes[i]);
                    writer.Write(";");
                    writer.WriteLine(c.changeLoads[i]);
                }
            }
            writer.Close();
#if SOLAROUTPUT
            writer = new StreamWriter(@"..\..\..\..\Output\solar\" + filename);
            foreach (double d in solaroutput)
            {
                writer.WriteLine(d);
            }
            writer.Close();
#endif

            writer = new StreamWriter(@"..\..\..\..\Output\readable\" + filename);
            foreach (double d in output.Values)
            {
                writer.WriteLine(d);
            }
            writer.Close();
            return output;
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
using MathNet.Numerics.Distributions;
using System.Reflection;
using static System.Collections.Specialized.BitVector32;

namespace OFS
{
    struct PriceChange(double time, double priceafter,bool firstencounter)
    {
        public double time = time;
        public double priceafter = priceafter;
        public bool firstencounter = firstencounter;
    }
    public abstract class Event(double time)
    {
        public double eventTime = time;

        public abstract void CallEvent();
    }
    public class EndSimulation(double time) : Event(time)
    {
        override public void CallEvent()
        {
            Program.simulation.EndSimulation();
            // Ik vraag me af wat we hier gaan doen
            // Moeten we alle data netjes afronden? Of kappen we het af?
            // Of misschien laten we het langszaam doodgaan (geen nieuwe autos bijvoorbeeld?)
        }
    }
    public class CarArrives(double time) : Event(time)
    {
        private int NextPrice(int interval)
        {
            switch (interval % 6)
            {
                case 0:
                case 1:
                    return 16;
                case 2:
                case 3:
                    return 18;
                case 4:
                    return 22;
                case 5:
                    return 20;
            }
            throw new Exception("Interval was not an integer");
        }
        private void PriceDriven(double eventTime, double endtime, double chargeTime, Station station)
        {
            // Implementing the algorithm in the report
            PriorityQueue<PriceChange, double> sweep = new PriorityQueue<PriceChange, Double>();

            int begininterval = (int) eventTime / 4; // round down
            int beginprice = NextPrice(begininterval);

            sweep.Enqueue(new PriceChange(eventTime, beginprice, true), eventTime);
            sweep.Enqueue(new PriceChange(eventTime + chargeTime, beginprice, false), eventTime + chargeTime);
            begininterval++;
            for (; 4 * begininterval < endtime; begininterval++)
            {
                if (begininterval == 1 || begininterval == 3)
                    continue; // there is no change here
                double afterprice = NextPrice(begininterval);
                sweep.Enqueue(new PriceChange(4 * begininterval, afterprice, true), 4 * begininterval);
                sweep.Enqueue(new PriceChange(4 * begininterval + chargeTime, afterprice, false), 4 * begininterval + chargeTime);
            }
            sweep.Enqueue(new PriceChange(endtime, 1000, true), endtime);
            sweep.Enqueue(new PriceChange(endtime + chargeTime, 1000, false), endtime + chargeTime);

            double price = 0;
            double previoustime = 0;
            double leftprice = 0; double rightprice = 0;
            double minprice = 10000000;
            double minleft = -1;
            double minright = -1;
            bool withinintervals = false;
            while (sweep.Count > 0)
            {
                var pc = sweep.Dequeue();
                price += (pc.time - previoustime) * (rightprice - leftprice);
                if (pc.firstencounter)
                {
                    rightprice = pc.priceafter;
                }
                if (!pc.firstencounter)
                {
                    withinintervals = true;
                    leftprice = pc.priceafter;
                }
                if (withinintervals && price + 1e-3 < minprice)
                {
                    minprice = price;
                    minright = pc.time;
                    minleft = pc.time - chargeTime;
                }
                previoustime = pc.time;
            }
            var car = new Car();
            Program.simulation.PlanEvent(new StartsCharging(car, station, minleft), minleft);
            Program.simulation.PlanEvent(new StopsCharging(car, station, minright), minright);
            Program.simulation.PlanEvent(new CarLeaves(station, endtime), endtime);
        }
        override public void CallEvent()
        {
            // We make a new CarArrives event for the next arriving car
            int hour = ((int)Math.Floor(eventTime)) % 24;
            double nextTime = RandomDists.PoissonSample(Data.ArrivalDistribution,eventTime);
            Program.simulation.PlanEvent(new CarArrives(nextTime), nextTime);
            Console.WriteLine(eventTime);

            // Now, we try to park the car at most three times
            List<int> triedParkings = new List<int>();
            bool emptyparkingfound = false;
            for (int attempt = 0; !emptyparkingfound && attempt < 3; attempt++)
            {
                // We generate a random parking spot. We must, however, make sure that we don't take one that we already tried
                int nextParking = -1;
                bool newParkingTried = false;
                while (!newParkingTried)
                {
                    // genrate a random parking spot using the CDF
                    nextParking = RandomDists.SampleCDF(Data.ParkingDistributionCumulative);
                    // We check if we have selected this parking before
                    newParkingTried = !triedParkings.Contains(nextParking);
                }
                Station station = Program.simulation.state.stations[nextParking];
                // Add car if there is capacity
                if (station.carCount < station.capacity)
                {
                    emptyparkingfound = true;
                    station.carCount++;
                    // We generate a charge volume and parking time
                    double chargeVolume = RandomDists.SampleContCDF(Data.ChargingVolumeCumulativeProbabilty);
                    double chargeTime = chargeVolume / 6; /// Assuming charging during just one interval
                    double parkingtime = RandomDists.SampleContCDF(Data.ConnectionTimeCumulativeProbabilty);
                    parkingtime = Math.Max(parkingtime, 1.4 * chargeTime); // to make sure it is lengthend if the parking time is too small.
                    
                    switch (Program.simulation.strategy)
                    {
                        case Strategy.ON_ARRIVAL:
                            var car = new Car();
                            Program.simulation.PlanEvent(new StartsCharging(car, station, eventTime), eventTime);
                            Program.simulation.PlanEvent(new StopsCharging(car, station, eventTime + chargeTime), eventTime + chargeTime);
                            Program.simulation.PlanEvent(new CarLeaves(station, eventTime + parkingtime), eventTime + parkingtime);
                            break;
                        case Strategy.PRICE_DRIVEN:
                            PriceDriven(eventTime, eventTime + parkingtime, chargeTime, station);
                            break;
                        case Strategy.FCFS:
                            // Todo
                            break;
                        case Strategy.ELFS:
                            // Todo
                            break;

                    }
                }
                // Add to tried parkings if no capacity
                else
                {
                    triedParkings.Add(nextParking);
                }
            }
            if (!emptyparkingfound)
            {
                Program.simulation.RejectCar();
            }
        }
    }
    public class StartsCharging(Car car, Station station, double time) : Event(time)
    {
        readonly Car car = car;
        readonly Station station = station;

        public override void CallEvent()
        { 
            station.ChangeParkingDemand(6, eventTime);
        }
    }
    public class StopsCharging(Car car, Station station, double time) : Event(time)
    {
        readonly Car car = car;
        readonly Station station = station;
        public override void CallEvent()
        {
            station.ChangeParkingDemand(-6, eventTime);
        }
    }
    public class CarLeaves(Station station, double time) : Event(time)
    {
        readonly Station station = station;
        public override void CallEvent()
        {
            station.carCount--;
        }
    }
    // TODO: Make sure all of the stations change output by the same amount
    // !!
    public class SolarPanelsChange(double time) : Event(time)
    {
        public override void CallEvent()
        {
            // take a random new output of the solar panels
            double averageoutput = 200*(Program.simulation.summer ? Data.SolarPanelAveragesSummer[((int)eventTime)%24] : Data.SolarPanelAveragesWinter[((int)eventTime) % 24]);
            double output = Normal.Sample(averageoutput, 0.15 * averageoutput);
#if SOLAROUTPUT
            Program.simulation.history.solaroutput.Add(output);
#endif
            foreach (int i in Program.simulation.solarStations)
            {
                Program.simulation.state.stations[i].SetSolarPanelOutput(output, eventTime);
            }

            // Enqueue next solar panel change
            Program.simulation.PlanEvent(new SolarPanelsChange(eventTime + 1), eventTime + 1);
        }
    }
}

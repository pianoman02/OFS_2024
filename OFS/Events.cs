using MathNet.Numerics.Distributions;

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
        private double PriceDriven(double eventTime, double endtime, double chargeTime)
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
                    minleft = pc.time - chargeTime;
                }
                previoustime = pc.time;
            }

            return minleft;
        }
        
        public double OptimalStartTime(double currentTime, double departureTime, double chargeTime)
        {
            switch (Program.simulation.strategy) {
                case Strategy.ON_ARRIVAL:
                    return currentTime;
                case Strategy.PRICE_DRIVEN:
                    return PriceDriven(currentTime, departureTime, chargeTime);
                default:
                    throw new Exception("This function should not be called in this scenario");
            }
        }
        override public void CallEvent()
        {
            // We make a new CarArrives event for the next arriving car
            int hour = ((int)Math.Floor(eventTime)) % 24;
            double nextTime = RandomDists.PoissonSample(Data.ArrivalDistribution,eventTime);
            Program.simulation.PlanEvent(new CarArrives(nextTime));

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

                    Car car = new(station);
                    // We generate a charge volume and parking time
                    double chargeTime = car.chargeVolume / Program.CHARGE_SPEED; /// Assuming charging during just one interval
                    double parkingTime = RandomDists.SampleContCDF(Data.ConnectionTimeCumulativeProbabilty);
                    parkingTime = Math.Max(parkingTime, 1.4 * chargeTime); // to make sure it is lengthend if the parking time is too small.
                    double departureTime = eventTime + parkingTime;

                    if (Program.simulation.strategy <= Strategy.PRICE_DRIVEN)
                    {
                        double startTime = OptimalStartTime(eventTime, departureTime, chargeTime);
                        Program.simulation.PlanEvent(new StartsCharging(car, startTime));
                    } else {
                        if (car.CanCharge()) {
                            Program.simulation.PlanEvent(new StartsCharging(car, eventTime));
                        }
                        else {
                            if (Program.simulation.strategy == Strategy.FCFS) {
                                car.prio = eventTime;
                            } else {
                                car.prio = departureTime - car.chargeVolume / Program.CHARGE_SPEED;
                            }
                            Program.simulation.Wait(car);
                        }
                    }

                    if (Program.simulation.strategy <= Strategy.PRICE_DRIVEN) {
                        Program.simulation.PlanEvent(new CarLeaves(station, departureTime));
                    } else {
                        Program.simulation.PlanEvent(new DesiredDeparture(car, departureTime));
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
    public class StartsCharging(Car car, double time) : Event(time)
    {
        readonly Car car = car;

        public override void CallEvent()
        {
            // We generate a random amount of charge, and schedule the moment it is detached
            double chargeTime = car.chargeVolume / Program.CHARGE_SPEED; /// Assuming greedy charging
            Program.simulation.PlanEvent(new StopsCharging(car, eventTime + chargeTime));
            car.station.ChangeParkingDemand(Program.CHARGE_SPEED, eventTime);
        }
    }
    public class StopsCharging(Car car, double time) : Event(time)
    {
        readonly Car car = car;
        public override void CallEvent()
        {
            car.fullyCharged = true;
            car.station.ChangeParkingDemand(-Program.CHARGE_SPEED, eventTime);
            if (car.timeToDepart) {
                Program.simulation.PlanEvent(new CarLeaves(car.station, eventTime));
            }

            Program.simulation.TryPlanNextCar(eventTime);
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
    public class DesiredDeparture(Car car, double time) : Event(time)
    {
        readonly Car car = car;
        public override void CallEvent()
        {
            car.timeToDepart = true;
            if (car.fullyCharged) {
                Program.simulation.PlanEvent(new CarLeaves(car.station, eventTime));
            }
        }
    }

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
            double old = double.MaxValue;
            foreach (int i in Program.simulation.solarStations)
            {
                old = Program.simulation.state.stations[i].solarPanelOutput;
                Program.simulation.state.stations[i].SetSolarPanelOutput(output, eventTime);
            }

            if (old < output) {
                Program.simulation.TryPlanNextCar(eventTime);
            }

            // Enqueue next solar panel change
            Program.simulation.PlanEvent(new SolarPanelsChange(eventTime + 1));
        }
    }
}

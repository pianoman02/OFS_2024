using MathNet.Numerics.Distributions;

namespace OFS
{
    public abstract class Event(double time)
    {
        public double eventTime = time;

        public abstract void CallEvent();
    }
    public class EndSimulation(double time) : Event(time)
    {
        override public void CallEvent()
        {
            Console.WriteLine(History.CarsRejected);
            Program.eventQueue.Clear();
            // Ik vraag me af wat we hier gaan doen
            // Moeten we alle data netjes afronden? Of kappen we het af?
            // Of misschien laten we het langszaam doodgaan (geen nieuwe autos bijvoorbeeld?)
        }
    }
    public class CarArrives(double time) : Event(time)
    {
        override public void CallEvent()
        {
            // We make a new CarArrives event for the next arriving car
            int hour = ((int)Math.Floor(eventTime)) % 24;
            double nextTime = Random.PoissonSample(Data.ArrivalDistribution,eventTime);
            Program.eventQueue.Enqueue(new CarArrives(nextTime), nextTime);
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
                    nextParking = Random.SampleCDF(Data.ParkingDistributionCumulative);
                    // We check if we have selected this parking before
                    newParkingTried = !triedParkings.Contains(nextParking);
                }
                Station station = Program.state.stations[nextParking];
                // Add car if there is capacity
                if (station.carCount < station.capacity)
                {
                    emptyparkingfound = true;
                    Program.eventQueue.Enqueue(new StartsCharging(new Car(), station, eventTime), eventTime);
                }
                // Add to tried parkings if no capacity
                else
                {
                    triedParkings.Add(nextParking);
                }
            }
            if (!emptyparkingfound)
            {
                History.CarsRejected++;
            }
        }
    }
    public class StartsCharging(Car car, Station station, double time) : Event(time)
    {
        readonly Car car = car;
        readonly Station station = station;

        public override void CallEvent()
        {
            station.carCount++;
            // We generate a random amount of charge, and schedule the moment it is detached
            double chargeVolume = Random.SampleContCDF(Data.ChargingVolumeCumulativeProbabilty);
            double chargeTime = chargeVolume / 6; /// Assuming greedy charging
            Program.eventQueue.Enqueue(new StopsCharging(car, station, eventTime + chargeTime), eventTime + chargeTime);
            // Schedule departure moment
            double parkingtime = Random.SampleContCDF(Data.ConnectionTimeCumulativeProbabilty);
            parkingtime = Math.Min(parkingtime, 1.4 * chargeTime); // to make sure it is lengthend if the parking time is too small.
            Program.eventQueue.Enqueue(new CarLeaves(station, eventTime + parkingtime), eventTime + parkingtime);
            // We change the charge
            station.ChangeParkingDemand(6, eventTime);
        }
    }
    public class StopsCharging(Car car, Station station, double time) : Event(time)
    {
        readonly Car car = car;
        readonly Station station = station;
        public override void CallEvent()
        {
            // todo
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
    public class SolarPanelsChange(Station station, double time) : Event(time)
    {
        readonly Station station = station;
        public override void CallEvent()
        {
            // take a random new output of the solar panels
            double averageoutput = Data.SolarPanelAverages[((int)eventTime)%24];
            double output = Normal.Sample(averageoutput, 0.15 * averageoutput);
            station.SetSolarPanelOutput(output, eventTime);

            // Enqueue next solar panel change
            Program.eventQueue.Enqueue(new SolarPanelsChange(station, eventTime + 1), eventTime + 1);
        }
    }
}

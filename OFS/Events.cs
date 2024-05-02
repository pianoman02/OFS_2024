using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFS
{
    public abstract class Event
    {
        public double EventTime;
        public abstract void CallEvent();
    }
    public class EndSimulation : Event
    {
        public EndSimulation(double time)
        {
            EventTime = time;
        }
        override public void CallEvent()
        {
            Console.WriteLine(History.CarsRejected);
            State.EventQueue.Clear();
            // Ik vraag me af wat we hier gaan doen
            // Moeten we alle data netjes afronden? Of kappen we het af?
            // Of misschien laten we het langszaam doodgaan (geen nieuwe autos bijvoorbeeld?)
        }
    }
    public class CarArrives : Event
    {
        public CarArrives(double time)
        {
            EventTime = time;
        }
        override public void CallEvent()
        {
            // We make a new CarArrives event for the next arriving car
            int hour = ((int)Math.Floor(EventTime)) % 24;
            double nextTime = Random.PoissonSample(Data.ArrivalDistribution,EventTime);
            State.EventQueue.Enqueue(new CarArrives(nextTime), nextTime);
            Console.WriteLine(EventTime);

            // Now, we try to park the car at most three times
            List<int> triedParkings = new List<int>();
            bool emptyparkingfound = false;
            for (int attempt = 0; !emptyparkingfound && attempt < 3; attempt++)
            {
                // We generate a random parking spot. We must, however, make sure that we don't take one that we already tried
                int nextparking = -1;
                bool newparkingtried = false;
                while (!newparkingtried)
                {
                    // genrate a random parking spot using the CDF
                    nextparking = Random.SampleCDF(Data.ParkingDistributionCumulative);
                    // We check if we have selected this parking before
                    newparkingtried = true;
                    foreach (int parking in triedParkings)
                    {
                        if (parking == nextparking)
                            newparkingtried = false;
                    }
                }
                // Add car if there is capacity
                if (State.CarsOnParking[nextparking] != Data.ParkingCapacities[nextparking])
                {
                    emptyparkingfound = true;
                    State.CarsOnParking[nextparking]++;
                    // We generate a random amount of charge, and schedule the moment it is detached
                    double chargevolume = Random.SampleContCDF(Data.ChargingVolumeCumulativeProbabilty);
                    double chargetime = chargevolume / 6; /// Assuming greedy charging
                    State.EventQueue.Enqueue(new StopsCharging(EventTime + chargetime, nextparking), EventTime + chargetime);
                    // Schedule departure moment
                    double parkingtime = Random.SampleContCDF(Data.ConnectionTimeCumulativeProbabilty);
                    parkingtime = Math.Min(parkingtime, 1.4 * chargetime); // to make sure it is lengthend if the parking time is too small.
                    State.EventQueue.Enqueue(new CarLeaves(EventTime + parkingtime, nextparking), EventTime + parkingtime);
                    // We change the charge
                    Cables.ChangeParkingDemand(nextparking, 6, EventTime);
                    
                }
                // Add to tried parkings if no capacity
                else
                {
                    triedParkings.Add(nextparking);
                }
            }
            if (!emptyparkingfound)
            {
                History.CarsRejected++;
            }
        }
    }
    public class StartsCharging : Event
    {
        public StartsCharging(double time)
        {
            EventTime = time;
        }
        public override void CallEvent()
        {
            // todo
        }
    }
    public class StopsCharging: Event
    {
        int parking;
        public StopsCharging(double time, int parking)
        {
            EventTime = time;
            this.parking = parking;
        }
        public override void CallEvent()
        {
            // todo
        }
    }
    public class CarLeaves: Event
    {
        int parking;
        public CarLeaves(double time, int parking)
        {
            EventTime = time;
            this.parking = parking;
        }
        public override void CallEvent()
        {
            State.CarsOnParking[parking]--;
        }
    }
    public class SolarPanelsChange: Event
    {
        int parking;
        public SolarPanelsChange(double time, int parking)
        {
            EventTime = time;
            this.parking = parking;
        }
        public override void CallEvent()
        {
            // take a random new output of the solar panels
            double averageoutput = Data.SolarPanelAverages[((int)EventTime)%24];
            double output = Normal.Sample(averageoutput, 0.15 * averageoutput);
            Cables.ChangeParkingDemand(parking, output - State.SolarPanelOutput[parking], EventTime);
            State.SolarPanelOutput[parking] = output;

            // Enqueue next solar panel change
            State.EventQueue.Enqueue(new SolarPanelsChange(EventTime + 1, parking), EventTime + 1);
        }
    }
}

﻿using System;

namespace OFS {
	public class Car
	{
		public bool fullyCharged = false;
		public bool timeToDepart = false;
	}

    public class Cable(int capacity, Cable? upstream = null)
    {
        public Cable? upstream = upstream; //The cable directly upstream from this cable. Null for the cable coming directly out of the transformer.
        public double load = 0; //current load of cable
		public int capacity = capacity;

        public List<double> changeLoads = []; //Different values for loads the cable has had.
        public List<double> changeTimes = []; //Times where the load has changed

        public void ChangeCableFlow(double powerChange, double time)
        {
            load += powerChange;
            changeLoads.Add(load);
            changeTimes.Add(time);
            upstream?.ChangeCableFlow(powerChange, time);
        }
    }

	public class Station(Cable cable, int capacity)
	{
		public Cable cable = cable;
		public double netCharge = 0;
		public int carCount = 0;
		public int capacity = capacity;
		public double solarPanelOutput = 0;

		public void ChangeParkingDemand(double powerChange, double time)
		{
			netCharge += powerChange;
			cable.ChangeCableFlow(powerChange, time);
		}
	}
	
}


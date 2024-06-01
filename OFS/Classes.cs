namespace OFS {
	public class Car(Station station)
	{
		public bool fullyCharged = false;
		public double? plannedDeparture = null;
		public double chargeVolume = RandomDists.SampleContCDF(Data.ChargingVolumeCumulativeProbabilty);
		public Station station = station;
		public double prio = 0;

		public bool CanCharge()
		{
			Cable? cable = station.cable;
			while (cable != null) {
				if (cable.capacity - cable.load - 6 < 0) {
					return false;
				}
				cable = cable.upstream;
			}
			return true;
		}
	}

    public class Cable(int capacity, Cable? upstream = null)
    {
        public Cable? upstream = upstream; //The cable directly upstream from this cable. Null for the cable coming directly out of the transformer.
        public double load = 0; //current load of cable
		private double realLoad = -1;
		public int capacity = capacity;

        public List<double> changeLoads = [0]; //Different values for loads the cable has had.
        public List<double> changeTimes = [0]; //Times where the load has changed

        public void ChangeCableFlow(double powerChange, double time)
        {
            load += powerChange;
            changeLoads.Add(load);
            changeTimes.Add(time);
            upstream?.ChangeCableFlow(powerChange, time);
        }

		public void ChangeVirtualCableFlow(double powerChange)
		{
			if (realLoad == -1) {
				realLoad = load;
			}
			load += powerChange;
			upstream?.ChangeVirtualCableFlow(powerChange);
		}

		public static void RestoreLoads()
		{
			foreach (Cable cable in Program.simulation.state.cables) {
				if (cable.realLoad != -1) {
					cable.load = cable.realLoad;
					cable.realLoad = -1;
				}
			}
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

		public void SetSolarPanelOutput(double output, double time)
		{
			ChangeParkingDemand(solarPanelOutput - output, time);
			solarPanelOutput = output;
		}
	}
}


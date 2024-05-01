using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFS
{
    static class Cables
    {
        public static void ChangeParkingDemand(int parking, double powerchange, double time)
        {
            State.NetChargeStation[parking] += powerchange;
            switch (parking)
            {
                // remember, the parkings are 0-based, so we must subtract 1 from the value in the figure
                case 0: ChangeCableFlow(2,powerchange, time); break;
                case 1: ChangeCableFlow(3,powerchange, time); break;
                case 2: ChangeCableFlow(4,powerchange, time); break;
                case 3: ChangeCableFlow(5,powerchange, time); break;
                case 4: ChangeCableFlow(8,powerchange, time); break;
                case 5: ChangeCableFlow(9,powerchange, time); break;
                case 6: ChangeCableFlow(6,powerchange, time); break;
            }
        }
        public static void ChangeCableFlow(int cable, double powerchange, double time)
        {

            State.CableLoad[cable] += powerchange;
            History.CableChangeLoads[cable].Add(State.CableLoad[cable]);
            History.CableChangeTimes[cable].Add(time);
            // Some checks whether it above or below the maximum cable demand

            switch (cable) {
                case 0: break; // starts at transformer
                case 1: ChangeCableFlow(0, powerchange, time); break;
                case 2: ChangeCableFlow(1, powerchange, time); break;
                case 3: ChangeCableFlow(1, powerchange, time); break;
                case 4: ChangeCableFlow(1, powerchange, time); break;
                case 5: ChangeCableFlow(0, powerchange, time); break;
                case 6: ChangeCableFlow(5, powerchange, time); break;
                case 7: ChangeCableFlow(5, powerchange, time); break;
                case 8: ChangeCableFlow(7, powerchange, time); break;
                case 9: ChangeCableFlow(7, powerchange, time); break;

            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Server
{
    class Counter
    {
        private System.Timers.Timer timer;
        private int timerValue;

        public Counter(int countTimer)
        {
            timer = new System.Timers.Timer(countTimer);
            timer.Elapsed += new ElapsedEventHandler(timerTick);
            timer.Enabled = false;
            timerValue = 0;
        }

        public Boolean TimerState
        {
            get { return timer.Enabled; }
            set { timer.Enabled = value; }
        }

        public int TimerValue
        {
            get { return timerValue; }
        }

        private void timerTick(object sender, ElapsedEventArgs e)
        {
            timerValue++;
        }
    }
}

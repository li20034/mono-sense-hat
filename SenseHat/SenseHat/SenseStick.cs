using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SenseHat
{
    /// <summary>
    /// Sense stick event arguments.
    /// </summary>
    public class SenseStickEventArgs : EventArgs
    {
        public readonly double timestamp; // Timestamp of event
        public readonly sbyte dir; // Direction of motion
        public readonly sbyte action; // Action committed

        /// <summary>
        /// Initializes a new instance of the <see cref="SenseHat.SenseStickEventArgs"/> class. Don't create your own.
        /// </summary>
        /// <param name="ts">Timestamp</param>
        /// <param name="d">Direction</param>
        /// <param name="act">Action</param>
        public SenseStickEventArgs(double ts, sbyte d, sbyte act) : base()
        {
            timestamp = ts;
            dir = d;
            action = act;
        }
    }

    /// <summary>
    /// Joystick Object for Raspberry Pi SenseHat
    /// </summary>
    public class SenseStick
    {
        // Libc instance of free
        [DllImport("libc.so.6")]
        private static extern void free(IntPtr ptr);

        // Open the linux device file for the joystick from the given dev name
        [DllImport("stick.so")]
        private static extern IntPtr open_sense_stick(string dev, sbyte exclusive);
        
        // Closes and frees all sense stick and all related resources
        [DllImport("stick.so")]
        private static extern void close_sense_stick(IntPtr dev);

        // Probe to discover the dev name for the joystick
        [DllImport("stick.so")]
        private static extern string probe_sense_stick();

        // Get the first unhandled event from joystick
        [DllImport("stick.so")]
        private static extern IntPtr get_sense_evt(IntPtr obj);

        /// <summary>
        /// Stick event structure imported from C drivers.
        /// ONLY INTERNAL, DO NOT USE ELSEWHERE
        /// </summary>
        private unsafe struct stickEvent
        {
            public double timestamp;
            public sbyte dir;
            public sbyte action;
        }

        IntPtr stick_dev; // Joystick device pointer
        Thread workerThd; // Thread for acquiring events : only active while not in manual mode
        private static bool instance = false; // Block multiple instances of object
        bool manual = false; // Manual event retrieval flag

        public const sbyte DIR_UP = 1, DIR_RIGHT = 2, DIR_DOWN = 3, DIR_LEFT = 4, DIR_MID = 5;
        public const sbyte ACTION_PRESS = 1, ACTION_RELEASE = 0, ACTION_HOLD = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="SenseHat.SenseStick"/> class.
        /// </summary>
        /// <param name="exclusive">Seize all control of joystick</param>
        /// <param name="manualPoll">Don't automatically retrieve events</param>
        public SenseStick(bool exclusive = true, bool manualPoll = false)
        {
            Init(exclusive, manualPoll);
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the <see cref="SenseHat.SenseStick"/> is
        /// reclaimed by garbage collection.
        /// </summary>
        ~SenseStick()
        {
            Free();
        }
        
        /// <summary>
        /// Initializes SenseStick internals
        /// </summary>
        /// <param name="exclusive">Seize all control of joystick</param>
        /// <param name="manualPoll">Don't automatically retrieve events</param>
        public void Init(bool exclusive = true, bool manualPoll = false) {
            if (instance) // Block new class creation if instance already exists
                throw new InvalidOperationException ("Multiple instances of SenseStick not permitted");

            instance = true;

            // Get device name of joystick
            string dev_name = probe_sense_stick();

            if (!string.IsNullOrEmpty(dev_name)) // If device name is valid, open Joystick device
                stick_dev = open_sense_stick(dev_name, (sbyte)(exclusive ? 1 : 0));
            else
                throw new InvalidOperationException("Cannot open sense stick");

            if (stick_dev == IntPtr.Zero)
                throw new InvalidOperationException("Cannot open sense stick");

            manual = manualPoll;
            // Start thread if manual flag isn't set
            if (!manualPoll)
                initWorker ();
        }
        
        /// <summary>
        /// Destroys this instance of SenseStick
        /// </summary>
        public void Free() {
            if (!instance)
                return;
            
            workerThd.Abort();
            close_sense_stick(stick_dev);
            instance = false;
        }
        
        /// <summary>
        /// Sets the manual mode.
        /// </summary>
        public void SetManualMode() {
            if (!manual) {
                manual = true;
                workerThd.Abort ();
            }
        }

        /// <summary>
        /// Sets the auto mode.
        /// </summary>
        public void SetAutoMode() {
            if (manual) {
                manual = false;
                initWorker ();
            }
        }

        /// <summary>
        /// Initializes and starts event retrieval worker.
        /// </summary>
        private void initWorker() {
            workerThd = new Thread(worker);
            workerThd.IsBackground = true;
            workerThd.Start();
        }

        /// <summary>
        /// Internal method for calling event handler
        /// </summary>
        /// <param name="e">Argument events</param>
        protected virtual void OnInputEvent(SenseStickEventArgs e)
        {
            EventHandler<SenseStickEventArgs> handler = InputEvent;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Occurs on input event.
        /// </summary>
        public event EventHandler<SenseStickEventArgs> InputEvent;

        /// <summary>
        /// Creates event arguments from imported C struct
        /// </summary>
        /// <returns>The sense stick event arguments.</returns>
        /// <param name="evt">Event in imported C struct</param>
        private SenseStickEventArgs makeSenseStickEventArgs(stickEvent evt)
        {
            return new SenseStickEventArgs(evt.timestamp, evt.dir, evt.action);
        }

        /// <summary>
        /// Background event retrieval worker
        /// </summary>
        private void worker()
        {
            unsafe
            {
                while (true)
                {
                    IntPtr evtPtr = get_sense_evt(stick_dev); // Blocks until event
                    if (evtPtr == IntPtr.Zero)
                        throw new InvalidOperationException ("Cannot retrieve event");

                    stickEvent evt = *(stickEvent*)evtPtr; // Interperets C struct as C# struct
                    SenseStickEventArgs e = makeSenseStickEventArgs(evt); // Create event arguments
                    free(evtPtr); // Free C event pointer

                    OnInputEvent(e); // Raise input event
                }
            }
        }

        /// <summary>
        /// Retrieves first unhandled event. Blocks if there isn't a stored event. Only works in manual mode
        /// </summary>
        /// <returns>Event arguments for retrieved events</returns>
        public SenseStickEventArgs GetEvent()
        {
            if (!manual) // Block if in auto mode
                throw new InvalidOperationException("Cannot call GetEvent in non-manual polling mode");

            unsafe
            {
                IntPtr evtPtr = get_sense_evt(stick_dev); // Blocks until event
                if (evtPtr == IntPtr.Zero)
                    throw new InvalidOperationException ("Cannot retrieve event");
                
                stickEvent evt = *(stickEvent*)evtPtr; // Interperets C struct as C# struct
                SenseStickEventArgs e = makeSenseStickEventArgs(evt); // Create event arguments
                free(evtPtr); // Free C event pointer

                return e; // Return input event
            }
        }
    }
}

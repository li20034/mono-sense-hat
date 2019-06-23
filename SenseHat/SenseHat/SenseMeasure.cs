using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SenseHat
{
    /// <summary>
    /// Environemental and Position Measurement Object for Raspberry Pi SenseHat
    /// </summary>
    public class SenseMeasure
    {
        private const string RTIMU_DEFAULT_SETTINGS_PATH = "/etc/RTIMULib"; // Global path to RTIMULib library config
        private const string RTIMU_LOCAL_SETTINGS_SFX = ".config/sense_hat/RTIMULib"; // Local user specfic RTIMU config file path template
        private const string RTIMU_LOCAL_SETTINGS_DIR_SFX = ".config/sense_hat"; // Local user specfic RTIMU config directory template

        [DllImport("rtimu_wrapper.so")]
        private static extern IntPtr initIMU(string config); // Initialize IMU

        [DllImport("rtimu_wrapper.so")]
        private static extern void freeIMU(IntPtr imuPtr); // Free IMU

        [DllImport("rtimu_wrapper.so")]
        private static extern IntPtr get_raw_data(IntPtr imuPtr, int selection); // Get raw data from IMU

        [DllImport("rtimu_wrapper.so")]
        private static extern IntPtr get_raw_humidity(IntPtr imuPtr); // Get raw data from humidity sensor

        [DllImport("rtimu_wrapper.so")]
        private static extern IntPtr get_raw_pres(IntPtr imuPtr); // Get raw data from pressure sensor

        [DllImport("rtimu_wrapper.so")]
        private static extern void set_imu_config(IntPtr imuPtr, bool compass_enabled, bool gyro_enabled, bool accel_enabled); // Enable/disable specific IMU sensors

        [DllImport("libc.so.6")]
        private static extern void free(IntPtr ptr); // free() from C library

        [DllImport("libc.so.6")]
        private static extern uint getuid(); // GNU/Linux getuid() syscall

        private static bool instance = false; // Block multiple instances of object

        // Enumerators representing data desired from RTIMU raw data
        private class RTIMU_Enums
        {
            public const int RAW_ACCEL = 1;
            public const int RAW_GYRO = 2;
            public const int RAW_FUSION_POSE = 3;
            public const int RAW_COMPASS = 4;
            public const int RAW_FUSIONQ_POSE = 5;
            public const int RAW_HUMIDITY = 6;
            public const int RAW_TEMP = 7;
            public const int RAW_PRES = 8;
            public const int RAW_TIMESTAMP = 9;
        }

        public SenseMeasure()
        {
            Init();
        }

        ~SenseMeasure()
        {
            Free();
        }

        private IntPtr imuPtr; // Pointer to RTIMU object

        /// <summary>
        /// Initialize measurement systems.
        /// </summary>
        public void Init()
        {
            if (instance)
                throw new InvalidOperationException ("Multiple instances of SenseMeasure not permitted");

            instance = true;
        
            if (!File.Exists(RTIMU_DEFAULT_SETTINGS_PATH + ".ini")) // Verify that global settings file exists
                throw new FileNotFoundException("Cannot find RTIMU global settings file @ " + RTIMU_DEFAULT_SETTINGS_PATH + ".ini");

            string homePath = Environment.GetEnvironmentVariable("HOME"); // Try to get home dir

            if (string.IsNullOrEmpty(homePath)) // Try even harder
            {
                if (getuid() == 0) // Make an exception for root. /home/root isn't valid on Debian
                    homePath = "/root";
                else
                    homePath = "/home/" + Environment.UserName;
            }

            string localCfgPath = Path.Combine(homePath, RTIMU_LOCAL_SETTINGS_SFX);

            if (!File.Exists(localCfgPath + ".ini"))
            {
                try // Try to copy global settings to local file
                {
                    Directory.CreateDirectory(Path.Combine(homePath, RTIMU_LOCAL_SETTINGS_DIR_SFX));
                    File.Copy(RTIMU_DEFAULT_SETTINGS_PATH + ".ini", localCfgPath + ".ini");
                }
                catch // Default to global file if operation fails
                {
                    Console.Error.WriteLine ("SenseMeasure_WARN: cannot copy settings locally, changes won't persist");
                    localCfgPath = RTIMU_DEFAULT_SETTINGS_PATH;
                }
            }

            imuPtr = initIMU(localCfgPath); // Initialize IMU & RTIMULib bindings

            if (imuPtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to initialize RTIMULib");
        }

        /// <summary>
        /// Frees resources used by the IMU object.
        /// </summary>
        public void Free()
        {
            if (!instance)
                return;
            
            freeIMU(imuPtr);
            instance = false;
        }

        /// <summary>
        /// Gets the temperature (from humidity sensor).
        /// </summary>
        /// <returns>The temperature in degrees Celcius.</returns>
        public double GetTemperature()
        {
            IntPtr dptr = get_raw_humidity(imuPtr);
            if (dptr == IntPtr.Zero)
                throw new InvalidOperationException("Temperature reading is not valid");

            // Copy data struct from c++ to c#
            double[] data = new double[2];
            Marshal.Copy(dptr, data, 0, 2); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return data[1]; // Extract temp
        }

        /// <summary>
        /// Gets the temperature from pressure sensor.
        /// </summary>
        /// <returns>The temperature pressure.</returns>
        public double GetTemperature_Pressure()
        {
            IntPtr dptr = get_raw_pres(imuPtr);
            if (dptr == IntPtr.Zero)
                throw new InvalidOperationException("Temperature reading is not valid");

            // Copy data struct from c++ to c#
            double[] data = new double[2];
            Marshal.Copy(dptr, data, 0, 2); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return data[1]; // Extract temp
        }

        /// <summary>
        /// Gets the humidity.
        /// </summary>
        /// <returns>The humidity.</returns>
        public double GetHumidity()
        {
            IntPtr dptr = get_raw_humidity(imuPtr);
            if (dptr == IntPtr.Zero)
                throw new InvalidOperationException("Humidity reading is not valid");

            // Copy data struct from c++ to c#
            double[] data = new double[2];
            Marshal.Copy(dptr, data, 0, 2); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return data[0]; // Extract humidity
        }

        /// <summary>
        /// Gets the pressure.
        /// </summary>
        /// <returns>The pressure.</returns>
        public double GetPressure()
        {
            IntPtr dptr = get_raw_pres(imuPtr);
            if (dptr == IntPtr.Zero)
                throw new InvalidOperationException("Pressure reading is not valid");

            // Copy data struct from c++ to c#
            double[] data = new double[2];
            Marshal.Copy(dptr, data, 0, 2); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return data[0]; // Extract pressure
        }

        /// <summary>
        /// Gets the orientation in degrees.
        /// </summary>
        /// <returns>double[] containing pitch, roll, yaw</returns>
        public double[] GetOrientation()
        {
            return GetOrientation_Degrees();
        }

        /// <summary>
        /// Gets the orientation in degrees.
        /// </summary>
        /// <returns>double[] containing pitch, roll, yaw</returns>
        public double[] GetOrientation_Degrees()
        {
            double[] radians = GetOrientation_Radians();
            double[] degrees = new double[3];

            for (int i = 0; i < 3; ++i)
            {
                degrees[i] = radians[i] / Math.PI * 180; // Convert rad to deg

                if (degrees[i] < 0) //map +-180 to 0 - 360
                    degrees[i] += 360;
            }

            return degrees;
        }

        /// <summary>
        /// Gets the orientation in radians.
        /// </summary>
        /// <returns>double[] containing pitch, roll, yaw</returns>
        public double[] GetOrientation_Radians()
        {
            IntPtr dptr = get_raw_data(imuPtr, RTIMU_Enums.RAW_FUSION_POSE);
            if (dptr == IntPtr.Zero)
                return null;

            // Copy data struct from c++ to c#
            double[] data = new double[3];
            Marshal.Copy(dptr, data, 0, 3); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return data;
        }

        /// <summary>
        /// Gets the compass reading.
        /// </summary>
        /// <returns>Direction of North in degrees.</returns>
        public double GetCompass()
        {
            Set_IMU_Config(true, false, false);

            return GetOrientation_Degrees()[2];
        }

        /// <summary>
        /// Enable and disable various components of the IMU sensor.
        /// </summary>
        /// <param name="compass_enabled">If set to <c>true</c> compass is enabled.</param>
        /// <param name="gyro_enabled">If set to <c>true</c> gyro is enabled.</param>
        /// <param name="accel_enabled">If set to <c>true</c> accel is enabled.</param>
        private void Set_IMU_Config(bool compass_enabled, bool gyro_enabled, bool accel_enabled)
        {
            set_imu_config(imuPtr, compass_enabled, gyro_enabled, accel_enabled);
        }

        /// <summary>
        /// Gets the orientation from gyroscope readings.
        /// </summary>
        /// <returns>double[] containing [x, y, z] rotation values in degrees.</returns>
        public double[] GetGyroscope()
        {
            Set_IMU_Config(false, true, false);

            return GetOrientation_Degrees();
        }

        /// <summary>
        /// Gets the orientation from accelerometer readings.
        /// </summary>
        /// <returns>double[] containing [x, y, z] rotation values in degrees.</returns>
        public double[] GetAccelerometer()
        {
            Set_IMU_Config(false, false, true);

            return GetOrientation_Degrees();
        }

        /// <summary>
        /// Gets the raw acceleration values from the accelerometer.
        /// </summary>
        /// <returns>double[] containing acceleration (in G's) in x, y, z directions.</returns>
        public double[] GetAccelerometer_Accel()
        {
            Set_IMU_Config(false, false, true);

            IntPtr dptr = get_raw_data(imuPtr, RTIMU_Enums.RAW_ACCEL);
            if (dptr == IntPtr.Zero)
                return null;

            // Copy data struct from c++ to c#
            double[] data = new double[3];
            Marshal.Copy(dptr, data, 0, 3); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return data;
        }

        /// <summary>
        /// Get fusion pose (orientation) from various IMU sensors (in degrees).
        /// </summary>
        /// <returns>The fusion pose in [pitch, roll, yaw].</returns>
        /// <param name="compass">If set to <c>true</c> compass is used.</param>
        /// <param name="gyro">If set to <c>true</c> gyro is used.</param>
        /// <param name="accel">If set to <c>true</c> accel is used.</param>
        public double[] GetFusion(bool compass, bool gyro, bool accel)
        {
            return GetFusion_Degrees(compass, gyro, accel);
        }
            
        /// <summary>
        /// Get fusion pose (orientation) from various IMU sensors (in degrees).
        /// </summary>
        /// <returns>The fusion pose in [pitch, roll, yaw].</returns>
        /// <param name="compass">If set to <c>true</c> compass is used.</param>
        /// <param name="gyro">If set to <c>true</c> gyro is used.</param>
        /// <param name="accel">If set to <c>true</c> accel is used.</param>
        public double[] GetFusion_Degrees(bool compass, bool gyro, bool accel)
        {
            Set_IMU_Config(compass, gyro, accel);

            return GetOrientation_Degrees();
        }
            
        /// <summary>
        /// Get fusion pose (orientation) from various IMU sensors (in radians).
        /// </summary>
        /// <returns>The fusion pose in [pitch, roll, yaw].</returns>
        /// <param name="compass">If set to <c>true</c> compass is used.</param>
        /// <param name="gyro">If set to <c>true</c> gyro is used.</param>
        /// <param name="accel">If set to <c>true</c> accel is used.</param>
        public double[] GetFusion_Radians(bool compass, bool gyro, bool accel)
        {
            Set_IMU_Config(compass, gyro, accel);

            return GetOrientation_Radians();
        }

        /// <summary>
        /// Gets the current timestamp from RTIMULib.
        /// </summary>
        /// <returns>The timestamp (double respresenting seconds).</returns>
        public double GetTimestamp()
        {
            IntPtr dptr = get_raw_data(imuPtr, RTIMU_Enums.RAW_TIMESTAMP);
            if (dptr == IntPtr.Zero)
                throw new InvalidOperationException("Timestamp is not valid");

            // Copy data struct from c++ to c#
            double[] timestamp = new double[1];
            Marshal.Copy(dptr, timestamp, 0, 1); // Somewhat equivalent to: void * memcpy(void * destination, const void * source, size_t num)
            free(dptr);

            return timestamp[0];
        }
    }
}

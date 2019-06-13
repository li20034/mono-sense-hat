#include <cstdlib>
#include <cstdio>
#include <iostream>
#include <unistd.h>
#include <RTIMULib.h>

#define RAW_ACCEL 1
#define RAW_GYRO 2
#define RAW_FUSION_POSE 3
#define RAW_COMPASS 4
#define RAW_FUSIONQ_POSE 5
#define RAW_HUMIDITY 6
#define RAW_TEMP 7
#define RAW_PRES 8
#define RAW_TIMESTAMP 9

using namespace std;

struct IMUState {
	RTIMUSettings* settings;
	RTPressure* pressure;
	RTHumidity* humidity;
	RTIMU* imu;
	unsigned int IMU_POLL_INTERVAL;
};

extern "C" IMUState* initIMU (const char* config) {
	IMUState* imuPtr = (IMUState*)malloc(sizeof(IMUState));
	if (!imuPtr) {
		//cerr << "Failed to allocate IMUState" << endl;
		return NULL;
	}
	
	imuPtr->settings = new RTIMUSettings(config); // initialize RTIMU config
	imuPtr->imu = RTIMU::createIMU(imuPtr->settings);
	imuPtr->pressure = RTPressure::createPressure(imuPtr->settings);	
	imuPtr->humidity = RTHumidity::createHumidity(imuPtr->settings);

    if (!imuPtr->imu->IMUInit()) {
        //cerr << "Failed to init IMU" << endl;
        return NULL;
    }
    
    // only init pressure and humidity sensors if main IMU inits. Prevents segfaults on fail
	imuPtr->pressure->pressureInit();
	imuPtr->humidity->humidityInit();

	imuPtr->IMU_POLL_INTERVAL = imuPtr->imu->IMUGetPollInterval(); // load default polling interval

	return imuPtr;
}

extern "C" void freeIMU (IMUState* imuPtr) {
	delete imuPtr->humidity;
	delete imuPtr->pressure;
	delete imuPtr->imu;
	delete imuPtr->settings;
	free(imuPtr);
}

extern "C" void set_imu_config (IMUState* imuPtr, bool compass_enabled, bool gyro_enabled, bool accel_enabled) {
	imuPtr->imu->setCompassEnable(compass_enabled);
	imuPtr->imu->setGyroEnable(gyro_enabled);
	imuPtr->imu->setAccelEnable(accel_enabled);
}

bool readIMU (IMUState* imuPtr) {
	for (int att = 0; att < 3; ++att) { // retry 3 times when reading IMU
		if (imuPtr->imu->IMURead())
			return true;
		usleep(imuPtr->IMU_POLL_INTERVAL * 1000); // wait for poll interval before retry
	}

	return false;
}

extern "C" double* get_raw_data (IMUState* imuPtr, unsigned char selection) { // TODO: optimize this by putting all data into a struct like the py version does
	double *result = (double*)malloc(4 * sizeof(double)); // allocate max needed space
    
    if (!result) // malloc failed
        return NULL;
    
	if (readIMU(imuPtr)) {
		const RTIMU_DATA& data = imuPtr->imu->getIMUData();
        char fail = 0;
        
		switch (selection) {
			case RAW_ACCEL:
				if (data.accelValid) {
					result[0] = data.accel.x();
					result[1] = data.accel.y();
					result[2] = data.accel.z();
				}
				else
	                fail = 1;
				break;
			case RAW_GYRO:
				if (data.gyroValid) {
					result[0] = data.gyro.x();
					result[1] = data.gyro.y();
					result[2] = data.gyro.z();
				}
				else
	                fail = 1;
				break;
			case RAW_COMPASS:
				if (data.compassValid) {
					result[0] = data.compass.x();
					result[1] = data.compass.y();
					result[2] = data.compass.z();
				}
			    else
	                fail = 1;
				break;
			case RAW_FUSION_POSE:
				if (data.fusionPoseValid) {
					result[0] = data.fusionPose.x();
					result[1] = data.fusionPose.y();
					result[2] = data.fusionPose.z();
				}
				else
	                fail = 1;
				break;
			case RAW_FUSIONQ_POSE:
				if (data.fusionQPoseValid) {
					result[0] = data.fusionQPose.scalar();
					result[1] = data.fusionQPose.x();
					result[2] = data.fusionQPose.y();
					result[3] = data.fusionQPose.z();
				}
				else
					fail = 1;
				break;
			case RAW_TIMESTAMP:
				result[0] = data.timestamp;
				break;
		}
		
		if (fail) { // check failure flag and free the ptr before returning NULL
		    free(result);
		    return NULL;
		}
		
		return result;
	}
	else
	    return NULL;
}

extern "C" double* get_raw_humidity (IMUState* imuPtr) {
	RTIMU_DATA data;
	imuPtr->humidity->humidityRead(data);
	
	if (!data.humidityValid || !data.temperatureValid)
		return NULL;
		
	double *result = (double*)malloc(2 * sizeof(double));
	if (!result) // malloc failed
        return NULL;
    
	result[0] = data.humidity;
	result[1] = data.temperature;
	
	return result;
}

extern "C" double* get_raw_pres (IMUState* imuPtr) {
	RTIMU_DATA data;
	imuPtr->pressure->pressureRead(data);
	
	if (!data.pressureValid || !data.temperatureValid)
		return NULL;
		
	double *result = (double*)malloc(2 * sizeof(double));
	if (!result) // malloc failed
        return NULL;
    
	result[0] = data.pressure;
	result[1] = data.temperature;
	
	return result;
}

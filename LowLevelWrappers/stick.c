//https://gist.github.com/uobikiemukot/457338b890e96babf60b
//https://git.kernel.org/pub/scm/linux/kernel/git/torvalds/linux.git/tree/include/uapi/linux/input-event-codes.h
#include <stdio.h>
#include <linux/input.h>
#include <linux/limits.h>
#include <fcntl.h>
#include <unistd.h>
#include <dirent.h>
#include <string.h>
#include <stdlib.h>

#define VAL_PRESS 1
#define VAL_RELEASE 0
#define VAL_HOLD 2

#define DIR_UP 1
#define DIR_RIGHT 2
#define DIR_DOWN 3
#define DIR_LEFT 4
#define DIR_MID 5

#define SENSE_INPUT_DEV_NAME "Raspberry Pi Sense HAT Joystick"

// Contains stick device information
struct stickDev {
    int fd;
    char exclusive;
};

// Contains stick events
struct stickEvent {
    double timestamp;
    char dir;
    char action;
};

// Discover dev path
char* probe_sense_stick() {
    int namelen = strlen(SENSE_INPUT_DEV_NAME);
    DIR* d = opendir("/sys/class/input");
    struct dirent* entry;
    char p[PATH_MAX + 1];
    char* p2 = NULL;
    char* evtname;
    
    // Scan directory tree for event*
    while ((entry = readdir(d)) != NULL) {
        if (!strncmp(entry->d_name, "event", 5)) {
            strcpy(p, "/sys/class/input/");
            strcat(p, entry->d_name);
            
            strcat(p, "/device/name");
            
            FILE* fp = fopen(p, "r"); // Open /sys/class/input/event*
            fseek(fp, 0, SEEK_END); // Seek to end
            
            long fsize = ftell(fp); // Get file size
            evtname = malloc(fsize + 1); // Allocate name string buf
            if (!evtname) // malloc (size_t size) failed
                return NULL;            
            
            fseek(fp, 0, SEEK_SET); // Seek to start
            fgets(evtname, fsize, fp); // Read name file until termination ('\n')
            fclose(fp); // Close name file
            
            evtname[strlen(evtname) - 1] = '\0'; // Remove last '\n'
            
            char res = !strcmp(evtname, SENSE_INPUT_DEV_NAME); // Compare name with known name of sense stick device
            free(evtname); // Free name buffer
            
            if (res) { // dev path was successfully found
                p2 = malloc(PATH_MAX + 1); // Allocate path buffer
                if (!p2) // malloc (size_t size) failed
                    return NULL;
                
                // build dev path of the sense stick dev in form of /dev/input/event*
                strcpy(p2, "/dev/input/");
                strcat(p2, entry->d_name);
                
                break;
            }
        }
    }
    closedir(d);
    
    return p2;
}

// Open sense stick device file
struct stickDev* open_sense_stick(char* dev, char exclusive) {
    usleep(75000); // Prevents "stuck key syndrome" on exclusive grab (race condition may occur otherwise)
    
    int fd = open(dev, O_RDONLY); // Open /dev/input/event* file
    if (fd == -1) // Check for error in opening file
        return NULL;
    
    struct stickDev* obj = malloc(sizeof(struct stickDev));
    if (!obj) // malloc (size_t size) failed
        return NULL;
    
    obj->fd = fd;
    
    if (exclusive) {      
        if (ioctl(fd, EVIOCGRAB, 1) == -1) { // Request exclusive grab
            close(fd); // Close dev input node
            free(obj); // Free object
            return NULL;
        }
        
        obj->exclusive = 1;
    }
    else
        obj->exclusive = 0;
    
    return obj;
}

// Properly closes and frees sense stick device resources
void close_sense_stick(struct stickDev* dev) {
    if (dev->exclusive)
        ioctl(dev->fd, EVIOCGRAB, 0); // Release exclusive grab (if held)
    
    close(dev->fd); // Close dev input node
    free(dev); // Free stickDev object
}

// Retrieves most recent event from device file
char get_sense_evt(struct stickDev* obj, struct stickEvent* e) {
    if (!e)
        return -1;

    struct input_event evt;
    evt.type = EV_SYN;
    
    while (evt.type != EV_KEY) { // Ignore events which are not EV_KEY since stick masquerades as kbd
        if (read(obj->fd, &evt, sizeof(struct input_event)) == -1) // Read event into default struct, blocks until event occurs
            return -1;
    }
    
    // Load data into custom event struct
    e->timestamp = evt.time.tv_sec + evt.time.tv_usec / 1000000.0; // Convert timeval struct to double timestamp (in seconds)
    switch (evt.code) {
        case KEY_UP:
            e->dir = DIR_UP;
            break;
        case KEY_DOWN:
            e->dir = DIR_DOWN;
            break;
        case KEY_LEFT:
            e->dir = DIR_LEFT;
            break;
        case KEY_RIGHT:
            e->dir = DIR_RIGHT;
            break;
        case KEY_ENTER:
            e->dir = DIR_MID;
            break;
    }
    
    e->action = evt.value;
    return 0;
}


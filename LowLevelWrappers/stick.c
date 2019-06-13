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

struct stickDev {
    int fd;
    char fail;      
};

struct stickEvent {
    double timestamp;
    char dir;
    char action;
};

char* probe_sense_stick() {
    int namelen = strlen(SENSE_INPUT_DEV_NAME);
    DIR* d = opendir("/sys/class/input");
    struct dirent* entry;
    char p[PATH_MAX + 1];
    char* p2 = NULL;
    char* evtname;
    
    //scan directory tree for event*
    while ((entry = readdir(d)) != NULL) {
        if (!strncmp(entry->d_name, "event", 5)) {
            strcpy(p, "/sys/class/input/");
            strcat(p, entry->d_name);
            
            //printf("found entry %s\n", p);
            
            strcat(p, "/device/name");
            
            FILE* fp = fopen(p, "r"); // open /sys/class/input/event*
            fseek(fp, 0, SEEK_END); // seek to end
            
            long fsize = ftell(fp); // get file size
            //printf("size: %d\n", fsize);
            evtname = malloc(fsize + 1); //allocate name string buf
            if (!evtname) // malloc fail!
                return NULL;            
            
            fseek(fp, 0, SEEK_SET); // seek to start
            fgets(evtname, fsize, fp); // read name until term char '\n'
            fclose(fp); // close
            
            evtname[strlen(evtname) - 1] = '\0'; // remove last '\n'
            //printf("probing %s: %s\n", p, evtname);
            
            char res = !strcmp(evtname, SENSE_INPUT_DEV_NAME); // compare name with known name of sense stick device
            free(evtname); // free name buf
            
            if (res) { // we've found the dev
                p2 = malloc(PATH_MAX + 1); // allocate path buffer
                if (!p2) // malloc fail!
                    return NULL;
                
                // build dev path (in form /dev/input/event*) of the sense stick dev
                strcpy(p2, "/dev/input/");
                strcat(p2, entry->d_name);
                
                //printf("found sense stick dev @ %s\n", p2);
                break;
            }
        }
    }
    closedir(d);
    
    return p2;
}

struct stickDev* open_sense_stick(char* dev, int exclusive) {
    usleep(75000); // UGLY HACK: prevents "stuck key syndrome" on exclusive grab
    
    struct stickDev* obj = malloc(sizeof(struct stickDev));
    if (!obj) // malloc fail!
        return NULL;
    
    int fd = open(dev, O_RDONLY); // open /dev/input/event* file
    
    if (fd == -1) {
        //perror("opening device");
        obj->fail = 1;
        
        return obj;
    }
    
    obj->fd = fd;
    
    if (exclusive) {
        if (ioctl(fd, EVIOCGRAB, 1) == -1) { // request exclusive grab
            //perror("grabbing exclusive access");
            obj->fail = 1;
            
            return obj;
        }
    }
    
    return obj;
}

struct stickEvent* get_sense_evt(struct stickDev* obj) {
    struct input_event evt;
    evt.type = EV_SYN;
    
    while (evt.type != EV_KEY) // ignore events which are not EV_KEY since stick masquerades as kbd
        read(obj->fd, &evt, sizeof(struct input_event)); // read event into default struct, blocks until event occurs
        
    struct stickEvent* e = malloc(sizeof(struct stickEvent)); // allocate custom event struct
    if (!e) // malloc fail!
        return NULL;
    
    // load data into custom event struct
    e->timestamp = evt.time.tv_sec + evt.time.tv_usec / 1000000; // convert timeval struct to double timestamp (in secs)
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
    return e;
}


import numpy as np
import matplotlib.pyplot as plt

file = open(r"Output/ELFS_summer_solar2_0.txt", "r")
file2 = open(r"Output/FCFS_summer_solar2_0.txt","r")

cabletimes = []
cableloads = []
cabletimes2 = []
cableloads2 = []


Lines = file.readlines()
linecounter = 0
rejectedCars = int(Lines[linecounter])
linecounter +=1
for cable in range(10):
    length = int(Lines[linecounter])
    linecounter+=1
    cabletimes.append(np.arange(length,dtype=float))
    cableloads.append(np.arange(length,dtype = float))
    for i in range(length):
        line = Lines[linecounter]
        linecounter+=1
        vals = line.split(";")
        cabletimes[cable][i]= float(vals[0].replace(',', '.'))
        cableloads[cable][i] = float(vals[1].replace(',', '.'))

file.close()

Lines = file2.readlines()
linecounter = 0
rejectedCars = int(Lines[linecounter])
linecounter +=1
for cable in range(10):
    length = int(Lines[linecounter])
    linecounter+=1
    cabletimes2.append(np.arange(length,dtype=float))
    cableloads2.append(np.arange(length,dtype = float))
    for i in range(length):
        line = Lines[linecounter]
        linecounter+=1
        vals = line.split(";")
        cabletimes2[cable][i]= float(vals[0].replace(',', '.'))
        cableloads2[cable][i] = float(vals[1].replace(',', '.'))

file.close()

plt.figure()
plt.step(cabletimes[1],cableloads[1],where='post') #this function makes steps
plt.step(cabletimes2[1],cableloads2[1],where='post') #this function makes steps
plt.step([0,1000], [200, 200], color="red")
plt.xlim(240,360)
plt.show()
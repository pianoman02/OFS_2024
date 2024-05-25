import numpy as np
import matplotlib.pyplot as plt

file = open(r"Output/ELFS_summer_solar0.txt","r")

cabletimes = []
cableloads = []


Lines = file.readlines()
linecounter = 0
rejectedCars = int(Lines[linecounter])
linecounter +=1
for cable in range(10):
    length = int(Lines[linecounter])
    linecounter+=1
    cabletimes.append(np.arange(length))
    cableloads.append(np.arange(length))
    for i in range(length):
        line = Lines[linecounter]
        linecounter+=1
        vals = line.split(";")
        cabletimes[cable][i]= float(vals[0])
        cableloads[cable][i] = float(vals[1])

file.close()

plt.figure()
plt.plot(cabletimes[0],cableloads[0])
plt.show()
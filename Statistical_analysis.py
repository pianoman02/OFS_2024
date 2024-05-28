import numpy as np
import matplotlib.pyplot as plt

file = open(r"Output/PRICE_DRIVEN_summer_solar1.txt","r")

cabletimes = []
cableloads = []


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
        cabletimes[cable][i]= float(vals[0])
        cableloads[cable][i] = float(vals[1])

file.close()

plt.figure()
plt.step(cabletimes[6],cableloads[6],where='post') #this function makes steps
plt.show()
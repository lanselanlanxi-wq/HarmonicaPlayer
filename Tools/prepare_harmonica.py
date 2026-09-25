"""Rebuild embedded samples from VCSL originals. Requires numpy/scipy.
Usage: python Tools/prepare_harmonica.py PATH_TO_ORIGINAL_WAVS
Original files and hashes: Assets/Harmonica/SOURCES.json.
"""
import sys,json,wave,hashlib
from pathlib import Path
from fractions import Fraction
import numpy as np
from scipy.signal import resample_poly, butter, sosfiltfilt, correlate
root=Path(__file__).resolve().parents[1]
original=Path(sys.argv[1]); dest=root/'Assets/Harmonica'
entries=[]
for name,midi in [('C3',60),('E3',64),('C4',72),('E4',76),('G4',79),('C5',84)]:
 p=original/f'Hohner-Special20_Normal_{name}.wav'
 with wave.open(str(p)) as w:
  assert (w.getnchannels(),w.getsampwidth(),w.getframerate())==(1,3,44100)
  b=np.frombuffer(w.readframes(w.getnframes()),np.uint8).reshape(-1,3).astype(np.int32)
 a=b[:,0]+(b[:,1]<<8)+(b[:,2]<<16);a=((a^8388608)-8388608)/8388608.
 target=440*2**((midi-69)/12)
 # Measure the fundamental near the expected note, rather than loud upper harmonics.
 n=1048576; segment=a[22050:88200];spectrum=abs(np.fft.rfft(segment*np.hanning(len(segment)),n))
 lo=int(target*.98*n/44100);hi=int(target*1.02*n/44100)
 k=lo+np.argmax(spectrum[lo:hi]);y=np.log(spectrum[k-1:k+2]);delta=.5*(y[0]-y[2])/(y[0]-2*y[1]+y[2]);freq=(k+delta)*44100/n
 ratio=Fraction(float(freq/target)).limit_denominator(4096)
 a=resample_poly(a,ratio.numerator,ratio.denominator)
 a=sosfiltfilt(butter(3,40,fs=44100,btype='highpass',output='sos'),a)
 # Limit ultrasonics before the small upward resampling used for adjacent notes.
 a=sosfiltfilt(butter(6,17000,fs=44100,output='sos'),a)
 onset=int(np.argmax(abs(a)>max(abs(a))*.035));trim=max(0,onset-88);a=a[trim:]
 rms=np.sqrt(np.mean(a[22050:66150]**2));gain=min(.16/rms,.85/max(abs(a)));a*=gain
 start=44100;cross=882
 template=a[start:start+cross]
 low=79380;high=105840
 corr=correlate(a[low:high+cross],template,mode='valid',method='fft')
 energy=np.convolve(a[low:high+cross]**2,np.ones(cross),mode='valid')
 score=corr/np.sqrt(np.maximum(energy*np.sum(template**2),1e-20))
 tail=low+int(np.argmax(score));end=tail+cross
 a=a[:end+2];a[:88]*=np.linspace(0,1,88)
 data=np.round(a*32767).astype('<i2')
 with wave.open(str(dest/f'{midi}.wav'),'wb') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(44100);w.writeframes(data.tobytes())
 entries.append(dict(midi=midi,loopStart=start,loopEnd=end,crossfade=cross,source=p.name,sourceSha256=hashlib.sha256(p.read_bytes()).hexdigest(),measuredHz=round(float(freq),6),targetHz=round(target,6),trimFrames=trim,gain=round(float(gain),6),loopCorrelation=round(float(score.max()),6)))
 print(midi,round(freq,3),'loop',start,end,'match',round(float(score.max()),3))
(dest/'SOURCES.json').write_text(json.dumps({'repository':'https://github.com/sgossner/VCSL','commit':'c1ea7bcc3c7309650ab0da9d15c9cd1fbc4a4c7e','sourceDirectory':'Aerophones/Free Aerophones/Harmonica-Hohner-Special20-C/Sustains/Normal','license':'CC0-1.0','samples':entries},indent=2)+'\n')

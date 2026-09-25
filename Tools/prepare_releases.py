"""python Tools/prepare_releases.py DIRECTORY_WITH_ORIGINAL_RELEASE_WAVS
Requires numpy and scipy only for rebuilding assets, not normal builds.
Uses the same pitch correction and gain as the paired sustain recording.
"""
from pathlib import Path
from fractions import Fraction
import sys,json,wave,hashlib
import numpy as np
from scipy.signal import resample_poly,butter,sosfiltfilt
root=Path(__file__).resolve().parents[1]/'Assets/Harmonica'
p=root/'SOURCES.json';j=json.loads(p.read_text())
for s in j['samples']:
 name=s['source'].replace('_Normal_', '_Normal_rel_')
 source=Path(sys.argv[1])/name
 with wave.open(str(source)) as w:
  assert (w.getnchannels(),w.getsampwidth(),w.getframerate())==(1,3,44100)
  b=np.frombuffer(w.readframes(w.getnframes()),np.uint8).reshape(-1,3).astype(np.int32)
 a=((b[:,0]+(b[:,1]<<8)+(b[:,2]<<16))^8388608)-8388608;a=a/8388608.
 ratio=Fraction(s['measuredHz']/s['targetHz']).limit_denominator(4096)
 a=resample_poly(a,ratio.numerator,ratio.denominator)
 a=sosfiltfilt(butter(3,40,fs=44100,btype='highpass',output='sos'),a)
 a=sosfiltfilt(butter(6,17000,fs=44100,output='sos'),a)*s['gain']
 # Preserve the release dynamics; do not normalize quiet breath noise upwards.
 a[-882:]*=.5+.5*np.cos(np.linspace(0,np.pi,882))
 data=np.round(np.clip(a,-.95,.95)*32767).astype('<i2')
 out=f"{s['midi']}-release.wav"
 with wave.open(str(root/out),'wb') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(44100);w.writeframes(data.tobytes())
 s['releaseFile']=out;s['releaseSource']=name;s['releaseSha256']=hashlib.sha256(source.read_bytes()).hexdigest()
 s['releaseFrames']=len(data)
 print(out,len(data))
j['releaseSourceDirectory']='Aerophones/Free Aerophones/Harmonica-Hohner-Special20-C/Releases/Normal'
p.write_text(json.dumps(j,indent=2)+'\n')

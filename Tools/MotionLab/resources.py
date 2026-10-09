"""Low-rate system samples; worker CUDA allocation and whole-GPU usage are distinct."""
import threading
import time
import subprocess
import psutil


class ResourceMonitor:
    def __init__(self):
        self.samples=[];self.stop_event=threading.Event();self.start=time.perf_counter()
        self.thread=threading.Thread(target=self.run,daemon=True)
    def __enter__(self):self.thread.start();return self
    def run(self):
        process=psutil.Process()
        while not self.stop_event.is_set():
            sample=dict(seconds=time.perf_counter()-self.start,rssBytes=process.memory_info().rss,availableRamBytes=psutil.virtual_memory().available)
            try:
                flags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess,'CREATE_NO_WINDOW') else 0
                result=subprocess.run(['nvidia-smi','--query-gpu=memory.used,utilization.gpu','--format=csv,noheader,nounits'],capture_output=True,text=True,timeout=3,creationflags=flags)
                values=result.stdout.strip().split(',')
                if result.returncode==0:sample.update(gpuTotalUsedMiB=int(values[0]),gpuUtilization=int(values[1]))
            except (OSError,ValueError,subprocess.TimeoutExpired):pass
            self.samples.append(sample);self.stop_event.wait(1)
    def __exit__(self,*args):self.stop_event.set();self.thread.join(timeout=4)

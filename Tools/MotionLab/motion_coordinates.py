"""One coordinate boundary. Never infer a reflection from an action caption."""
import numpy as np

def root_quaternions(features):
    data=np.asarray(features)
    angle=np.cumsum(np.r_[0,data[:-1,0]])
    # Official recovery stores world->local wxyz. Invert, then S R S with
    # S=diag(-1,1,1), then serialize Unity xyzw.
    return np.stack([np.zeros_like(angle),np.sin(angle),np.zeros_like(angle),np.cos(angle)],axis=-1)

def reflect_rotation(matrix):
    s=np.diag([-1.,1.,1.]);return s@np.asarray(matrix)@s

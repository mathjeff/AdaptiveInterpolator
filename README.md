AdaptiveInterpolator
by Jeff Gaston

The AdaptiveInterpolater was created to performs numerical interpolation for multidimensional data. It's essentially an R tree.

The caller chooses a number of dimensions in the input space, and provides several data points by calling AddDatapoint. When the caller invokes Interpolate, then the interpolator categorizes the the data points and attempts to fit a somewhat smooth surface to them, and uses the value of the surface for that input to generate the output.

Currently, the implementation is a binary tree that splits in different dimensions according to the shape of the data. At some point in the future it may be worth implementing a linear interpolator after having found a region from which to interpolate.
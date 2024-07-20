AdaptiveInterpolator
by Jeff Gaston

AdaptiveInterpolater was created to predict the value of a function based on example data. It's essentially an [R tree](https://en.wikipedia.org/wiki/R-tree) that predicts each value to be the average of the points in its box.

Interesting attributes of AdaptiveInterpolator:

1. It adjusts how far to split a node [based on](https://github.com/mathjeff/AdaptiveInterpolator/blob/master/Source/Lazy/LazyDimension_Interpolator.cs#L83) how good the predictions are.

  * When predictions are good, then noise is small, and there are more splits and better predictions.

  * When predictions are bad, then noise is large, and there are fewer splits so we can better estimate the noise.

2. It evaluates dimension values [lazily](https://github.com/mathjeff/AdaptiveInterpolator/blob/master/Source/Lazy/LazyDimension_Interpolator.cs#L17)

  * This can help when computing the value of a specific dimension is expensive.

  * AdaptiveInterpolator can handle [thousands of dimensions](http://github.com/mathjeff/ActivityRecommender) and tens of thousands of datapoints on a phone in under a second

3. Each split is [a vote](https://github.com/mathjeff/AdaptiveInterpolator/blob/master/Source/Lazy/LazyDimension_InterpolatorBox.cs#L87) of several dimensions

  * This can help when individual dimensions are independently noisy.

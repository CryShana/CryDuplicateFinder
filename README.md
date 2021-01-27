# CryDuplicateFinder
Windows-only image duplicate finder GUI. Optimized for batch processing and deleting/moving duplicates.

## Usage
- Select root directory (Program will find all image files in all subdirectories)
- Select max. threads to properly utilize your CPU (by default set to [Cores]*2)
- Select duplicate checking method
  - **Histogram** (compares colors, really **fast**) - recommended when trying to find identical duplicates
  - **Features** (uses ORB feature matching, **slow**) - recommended when trying to find duplicates that are differently sized, rotated, colored.
  
 ## Features
 - Finds duplicates / similar images using multiple methods
 - Can process thousands of images (searches the entire root directory)
 - Can delete OR move duplicated images (either specific ones or all of them)

### Screenshots / Video
![Screenshot1](https://cryshana.me/f/gotQvMnpLGtm.png)
![Screenshot2](https://cryshana.me/f/lyVBEkmbXiTq.png)

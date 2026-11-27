#import <UIKit/UIKit.h>

float SaiGameBannerPixelsPerPoint(void)
{
    return (float)[UIScreen mainScreen].nativeScale;
}

float SaiGameBannerCustomOffsetScale(void)
{
    UIScreen *screen = [UIScreen mainScreen];
    return (float)(screen.nativeScale / screen.scale);
}

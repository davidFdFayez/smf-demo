namespace SMF.Domain.Enums;

/// <summary>Push device family. iOS/Android map to FCM tokens issued by the
/// respective Firebase SDK on the Flutter app; Web maps to FCM JS / VAPID.</summary>
public enum DevicePlatform
{
    iOS     = 1,
    Android = 2,
    Web     = 3
}

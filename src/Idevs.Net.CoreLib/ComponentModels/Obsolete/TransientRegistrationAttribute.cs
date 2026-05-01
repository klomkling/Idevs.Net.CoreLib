namespace Idevs.ComponentModel;

[AttributeUsage(AttributeTargets.Class)]
[Obsolete("Use Idevs.ComponentModels.TransientAttribute instead. This legacy attribute will be removed in a future major version.", false)]
public class TransientRegistrationAttribute : Attribute;

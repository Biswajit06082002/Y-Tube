namespace User.Entities.Contracts;

/// <param name="IsDefault">True when the user never saved anything (the frontend then keeps the guest's local choice).</param>
public sealed record PreferencesDto(string Theme, bool IsDefault);

public sealed record UpdatePreferencesRequest(string Theme);

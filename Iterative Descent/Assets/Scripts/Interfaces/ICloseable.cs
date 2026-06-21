/// <summary>
/// Implemented by any overlay that can be dismissed via the centralised ESC handler
/// in PlayerInteractor. Register with PlayerInteractor.RegisterCloseable(this) when
/// the overlay opens; call PlayerInteractor.DeregisterCloseable() inside Close().
/// </summary>
public interface ICloseable
{
    void Close();
}

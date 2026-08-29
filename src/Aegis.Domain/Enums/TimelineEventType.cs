namespace Aegis.Domain.Enums;

public enum TimelineEventType
{
    InvestigationCreated = 0,
    AssetAdded = 1,
    AssetRemoved = 2,
    AnnotationAdded = 3,
    Note = 4,
    GeofenceAlert = 5,
    FlightEntered = 6,
    FlightExited = 7,
    GeocodeResolved = 8,
    OsintSourceAccess = 9
}

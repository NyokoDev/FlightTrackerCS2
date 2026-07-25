import React, {
    useEffect,
    useMemo,
    useRef,
    useState
} from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import mod from "../../../mod.json";

import FlightRadar from "./FlightRadar/FlightRadar";
import "./FlightTrackerRadar.scss";
import { createPortal } from "react-dom";

/*
Expected C# string format:

entityIndex|entityVersion|name|status|altitude|speed
Example:
421|1|Boeing 737-800|Airborne|1250.5|82.4
*/

interface MainUIProps {
    onClose: () => void;
}

interface FlightRowProps {
    flight: TrackedFlight;
    isFocused: boolean;
    onClick: () => void;
}

export interface TrackedFlight {
    entityIndex: number;
    entityVersion: number;
    name: string;
    status: string;
    altitude: number;
    speed: number;
}

// Array of serialized aircraft information.
export const TrackedFlights$ = bindValue<string[]>(
    mod.id,
    "TrackedFlights",
    []
);

// Number of tracked aircraft.
export const TrackedFlightCount$ = bindValue<number>(
    mod.id,
    "TrackedFlightCount",
    0
);

// Currently selected aircraft.
export const SelectedFlight$ = bindValue<string>(
    mod.id,
    "SelectedFlight",
    ""
);

// Currently selected aircraft information.
export const SelectedFlightInfo$ = bindValue<string>(
    mod.id,
    "SelectedFlightInfo",
    ""
);

export default function FlightTracker({ onClose }: MainUIProps) {
    const rawFlights = useValue(TrackedFlights$);
    const flightCount = useValue(TrackedFlightCount$);
    const selectedFlight = useValue(SelectedFlight$);
    const panelRef = useRef<HTMLDivElement>(null);

    const [showRadar, setShowRadar] = useState(false);

    const [position, setPosition] = useState({
        x: 20,
        y: 20
    });

    const dragOffset = useRef({
        x: 0,
        y: 0
    });

    const dragging = useRef(false);

    useEffect(() => {
        const handleMouseMove = (event: MouseEvent) => {
            if (!dragging.current)
                return;

            setPosition({
                x: event.clientX - dragOffset.current.x,
                y: event.clientY - dragOffset.current.y
            });
        };

        const handleMouseUp = () => {
            dragging.current = false;
        };

        window.addEventListener(
            "mousemove",
            handleMouseMove
        );

        window.addEventListener(
            "mouseup",
            handleMouseUp
        );

        return () => {
            window.removeEventListener(
                "mousemove",
                handleMouseMove
            );

            window.removeEventListener(
                "mouseup",
                handleMouseUp
            );
        };
    }, []);

    const beginDrag = (
        event: React.MouseEvent<HTMLDivElement>
    ) => {
        if (event.button !== 0)
            return;

        if (!panelRef.current)
            return;

        const rect =
            panelRef.current.getBoundingClientRect();

        dragging.current = true;

        dragOffset.current = {
            x: event.clientX - rect.left,
            y: event.clientY - rect.top
        };

        event.preventDefault();
    };

    const flights = useMemo<TrackedFlight[]>(() => {
        return rawFlights
            .map(parseTrackedFlight)
            .filter(
                (
                    flight
                ): flight is TrackedFlight =>
                    flight !== null
            );
    }, [rawFlights]);

    return (
        <div
            ref={panelRef}
            className="flightTrackerRadar"
            onMouseDown={beginDrag}
            style={{
                left: `${position.x}px`,
                top: `${position.y}px`,
                transform: "none"
            }}
        >
            <header className="flightTrackerRadar__header">
                <div>
                    <div className="flightTrackerRadar__title">
                        Flight Tracker Radar
                    </div>

                    <div className="flightTrackerRadar__subtitle">
                        Active aircraft
                    </div>
                </div>

                <div className="flightTrackerRadar__count">
                    {flightCount}
                </div>


<button
    type="button"
    className="flightTrackerRadar__radarButton"
    onMouseDown={(event) => {
        event.stopPropagation();
    }}
    onClick={(event) => {
        event.stopPropagation();
        setShowRadar(true);
    }}
>
    Open Radar
</button>

<button
            type="button"
            className="flightTrackerRadar__closeButton"
            onMouseDown={(event) => {
                event.stopPropagation();
            }}
            onClick={(event) => {
                event.stopPropagation();
                onClose();
            }}
            aria-label="Close Flight Tracker"
            title="Close"
        >
            ×
        </button>

            </header>

            <div className="flightTrackerRadar__list">
                {flights.length === 0 ? (
                    <div className="flightTrackerRadar__empty">
                        No active aircraft
                    </div>
                ) : (
                    flights.map((flight) => {
    const flightId =
        `${flight.entityIndex}|${flight.entityVersion}`;

    return (
        <FlightRow
            key={`${flight.entityIndex}-${flight.entityVersion}`}
            flight={flight}
            isFocused={selectedFlight === flightId}
            onClick={() =>
                trigger(
                    mod.id,
                    "FocusAircraft",
                    flightId
                )
            }
        />
    );
})
                )}

                 {showRadar &&
                createPortal(
                    <FlightRadar
                        onClose={() =>
                            setShowRadar(false)
                        }
                    />,
                    document.body
                )}
            </div>

                 
       

        </div>

        
    );
}



function FlightRow({
    flight,
    isFocused,
    onClick
}: FlightRowProps) {
    return (
        <div
            className={[
                "flightTrackerRadar__flight",
                isFocused
                    ? "flightTrackerRadar__flight--focused"
                    : ""
            ]
                .filter(Boolean)
                .join(" ")}
            onClick={onClick}
        >
            <div className="flightTrackerRadar__flightMain">
                <span className="flightTrackerRadar__aircraftName">
                    {flight.name}
                </span>

                <div className="flightTrackerRadar__badges">
                    {isFocused && (
                        <span className="flightTrackerRadar__focusedBadge">
                            Focused
                        </span>
                    )}

                    <span
                        className={`flightTrackerRadar__status ${getStatusClass(
                            flight.status
                        )}`}
                    >
                        {flight.status}
                    </span>
                </div>
            </div>

            <div className="flightTrackerRadar__telemetry">
                <span>
                    Altitude:{" "}
                    {formatNumber(
                        flight.altitude,
                        0
                    )}{" "}
                    m
                </span>

                <span>
                    Speed:{" "}
                    {formatNumber(
                        flight.speed,
                        1
                    )}
                </span>


                
            </div>
        


        </div>

        
    );
}

function parseTrackedFlight(
    value: string
): TrackedFlight | null {
    if (!value) {
        console.warn(
            "[FlightTracker] Received empty flight string"
        );

        return null;
    }

    const parts = value.split("|");

    console.log(
        "[FlightTracker] Parsing:",
        value,
        "fields:",
        parts.length
    );

    if (parts.length !== 9) {
        console.warn(
            `[FlightTracker] Expected 9 fields but received ${parts.length}:`,
            parts
        );

        return null;
    }

    const entityIndex =
        Number.parseInt(parts[0], 10);

    const entityVersion =
        Number.parseInt(parts[1], 10);

    const speed =
        Number.parseFloat(parts[7]);

    const altitude =
        Number.parseFloat(parts[8]);

    if (
        !Number.isFinite(entityIndex) ||
        !Number.isFinite(entityVersion) ||
        !Number.isFinite(speed) ||
        !Number.isFinite(altitude)
    ) {
        console.warn(
            "[FlightTracker] Invalid numeric flight data:",
            {
                value,
                entityIndex,
                entityVersion,
                speed,
                altitude
            }
        );

        return null;
    }

    return {
        entityIndex,
        entityVersion,
        name:
            parts[2]?.trim() ||
            "Unknown Aircraft",
        status:
            parts[3]?.trim() ||
            "Unknown",
        altitude,
        speed
    };
}

function getStatusClass(
    status: string
): string {
    const normalizedStatus = status
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");

    return `flightTrackerRadar__status--${
        normalizedStatus || "unknown"
    }`;
}

function formatNumber(
    value: number,
    decimalPlaces: number
): string {
    if (!Number.isFinite(value))
        return "0";

    return value.toFixed(decimalPlaces);
}
import React, { useEffect, useMemo, useRef, useState } from "react";
import { bindValue, useValue } from "cs2/api";
import mod from "../../../../mod.json";
import "./FlightRadar.scss";


import AircraftSVG from "../../../images/Aircraft.svg";

interface AnimatedRadarPosition {
    left: number;
    top: number;
    velocityX: number;
    velocityY: number;
}

export const TrackedFlights$ = bindValue<string[]>(
    mod.id,
    "RadarFlights",
    []
);

interface FlightRadarProps {
    onClose: () => void;
}

interface RadarAircraft {
    id: string;
    name: string;
    altitude: number;
    speed: number;
    status: string;
    x: number;
    z: number;
}

function parseRadarAircraft(value: string): RadarAircraft | null {
    const parts = value.split("|");

    if (parts.length < 7) {
        return null;
    }

    const altitude = Number(parts[2]);
    const speed = Number(parts[3]);
    const x = Number(parts[5]);
    const z = Number(parts[6]);

    if (
        !Number.isFinite(altitude) ||
        !Number.isFinite(speed) ||
        !Number.isFinite(x) ||
        !Number.isFinite(z)
    ) {
        return null;
    }

    return {
        id: parts[0],
        name: parts[1],
        altitude,
        speed,
        status: parts[4],
        x,
        z
    };
}

export default function FlightRadar({
    onClose
}: FlightRadarProps) {
    const trackedFlights = useValue(TrackedFlights$) ?? [];
const [animatedPositions, setAnimatedPositions] = useState<
    Record<string, AnimatedRadarPosition>
>({});

    const FLRadarRef = useRef<HTMLDivElement>(null);

    const [FLRadarPosition, setFLRadarPosition] = useState({
        x: 180,
        y: 120
    });

    const FLRadarDragOffset = useRef({
        x: 0,
        y: 0
    });

    const [selectedAircraftId, setSelectedAircraftId] =
        useState<string | null>(null);

    const aircraft = useMemo(() => {
        return trackedFlights
            .map(parseRadarAircraft)
            .filter(
                (flight): flight is RadarAircraft =>
                    flight !== null
            );
    }, [trackedFlights]);

    const mapBounds = useMemo(() => {
        if (aircraft.length === 0) {
            return {
                minX: -500,
                maxX: 500,
                minZ: -500,
                maxZ: 500
            };
        }



        let minX = Math.min(...aircraft.map(flight => flight.x));
        let maxX = Math.max(...aircraft.map(flight => flight.x));
        let minZ = Math.min(...aircraft.map(flight => flight.z));
        let maxZ = Math.max(...aircraft.map(flight => flight.z));

        const centerX = (minX + maxX) / 2;
        const centerZ = (minZ + maxZ) / 2;

        const minimumRange = 1000;

        const rangeX = Math.max(maxX - minX, minimumRange);
        const rangeZ = Math.max(maxZ - minZ, minimumRange);

        const largestRange = Math.max(rangeX, rangeZ);
        const padding = largestRange * 0.15;
        const halfSize = largestRange / 2 + padding;

        minX = centerX - halfSize;
        maxX = centerX + halfSize;
        minZ = centerZ - halfSize;
        maxZ = centerZ + halfSize;

        return {
            minX,
            maxX,
            minZ,
            maxZ
        };
    }, [aircraft]);

    useEffect(() => {
    setAnimatedPositions(previous => {
        const updated: Record<string, AnimatedRadarPosition> = {};

        aircraft.forEach((flight, index) => {
            const existing = previous[flight.id];

            if (existing) {
                updated[flight.id] = existing;
                return;
            }

            const width = mapBounds.maxX - mapBounds.minX;
            const height = mapBounds.maxZ - mapBounds.minZ;

            const normalizedX =
                (flight.x - mapBounds.minX) / width;

            const normalizedZ =
                (flight.z - mapBounds.minZ) / height;

            const initialLeft = Math.min(
                90,
                Math.max(10, normalizedX * 100)
            );

            const initialTop = Math.min(
                90,
                Math.max(10, (1 - normalizedZ) * 100)
            );

            /*
             * Give every aircraft a slightly different fictional
             * direction and speed.
             */
            const angle =
                ((index * 137.5 + 35) * Math.PI) / 180;

            const speed = 0.025 + (index % 4) * 0.008;

            updated[flight.id] = {
                left: initialLeft,
                top: initialTop,
                velocityX: Math.cos(angle) * speed,
                velocityY: Math.sin(angle) * speed
            };
        });

        return updated;
    });
}, [aircraft, mapBounds]);

useEffect(() => {
    const movementInterval = window.setInterval(() => {
        setAnimatedPositions(previous => {
            const updated: Record<string, AnimatedRadarPosition> = {};

            for (const [id, position] of Object.entries(previous)) {
                let nextLeft =
                    position.left + position.velocityX;

                let nextTop =
                    position.top + position.velocityY;

                let nextVelocityX = position.velocityX;
                let nextVelocityY = position.velocityY;

                /*
                 * Keep the aircraft inside the radar with room
                 * for the icon and label.
                 */
                const minimumPosition = 7;
                const maximumPosition = 93;

                if (
                    nextLeft <= minimumPosition ||
                    nextLeft >= maximumPosition
                ) {
                    nextVelocityX *= -1;

                    nextLeft = Math.min(
                        maximumPosition,
                        Math.max(minimumPosition, nextLeft)
                    );
                }

                if (
                    nextTop <= minimumPosition ||
                    nextTop >= maximumPosition
                ) {
                    nextVelocityY *= -1;

                    nextTop = Math.min(
                        maximumPosition,
                        Math.max(minimumPosition, nextTop)
                    );
                }

                updated[id] = {
                    left: nextLeft,
                    top: nextTop,
                    velocityX: nextVelocityX,
                    velocityY: nextVelocityY
                };
            }

            return updated;
        });
    }, 30);

    return () => {
        window.clearInterval(movementInterval);
    };
}, []);

    const worldToRadar = (x: number, z: number) => {
        const width = mapBounds.maxX - mapBounds.minX;
        const height = mapBounds.maxZ - mapBounds.minZ;

        const normalizedX = (x - mapBounds.minX) / width;
        const normalizedZ = (z - mapBounds.minZ) / height;

        return {
            left: `${normalizedX * 100}%`,
            top: `${(1 - normalizedZ) * 100}%`
        };
    };

    const FLRadarPointerDown = (
        event: React.PointerEvent<HTMLDivElement>
    ) => {
        const target = event.target as HTMLElement;

        if (
            target.closest("button") ||
            target.closest(".FLRadarAircraft")
        ) {
            return;
        }

        FLRadarDragOffset.current = {
            x: event.clientX - FLRadarPosition.x,
            y: event.clientY - FLRadarPosition.y
        };

        event.currentTarget.setPointerCapture(event.pointerId);
    };

    const FLRadarPointerMove = (
        event: React.PointerEvent<HTMLDivElement>
    ) => {
        if (!event.currentTarget.hasPointerCapture(event.pointerId)) {
            return;
        }

        setFLRadarPosition({
            x: event.clientX - FLRadarDragOffset.current.x,
            y: event.clientY - FLRadarDragOffset.current.y
        });
    };

    const FLRadarPointerUp = (
        event: React.PointerEvent<HTMLDivElement>
    ) => {
        if (event.currentTarget.hasPointerCapture(event.pointerId)) {
            event.currentTarget.releasePointerCapture(event.pointerId);
        }
    };

    return (
        <div
            ref={FLRadarRef}
            className="FLRadar"
            style={{
                left: FLRadarPosition.x,
                top: FLRadarPosition.y
            }}
            onPointerDown={FLRadarPointerDown}
            onPointerMove={FLRadarPointerMove}
            onPointerUp={FLRadarPointerUp}
            onPointerCancel={FLRadarPointerUp}
        >
            <button
                className="FLRadarCloseButton"
                onClick={onClose}
            >
                ×
            </button>

            <div className="FLRadarMapPlaceholder">
  {aircraft.map(flight => {
    const animatedPosition =
        animatedPositions[flight.id];

    const fallbackPosition = worldToRadar(
        flight.x,
        flight.z
    );

    const left = animatedPosition
        ? `${animatedPosition.left}%`
        : fallbackPosition.left;

    const top = animatedPosition
        ? `${animatedPosition.top}%`
        : fallbackPosition.top;

    const heading = animatedPosition
    ? Math.atan2(
          animatedPosition.velocityY,
          animatedPosition.velocityX
      ) *
          (180 / Math.PI)
    : 0;

    const selected =
        selectedAircraftId === flight.id;

    return (

        <div
    key={flight.id}
    className="FLRadarAircraft"
    style={{
        left,
        top
    }}
    onPointerDown={event => {
        event.stopPropagation();
    }}
    title={`${flight.name}
Altitude: ${flight.altitude.toFixed(0)} m
Speed: ${flight.speed.toFixed(1)}
Status: ${flight.status}`}
>
    <img
        className="FLRadarAircraftIcon"
        src={AircraftSVG}
        alt=""
        draggable={false}
        style={{
            transform: `rotate(${heading}deg)`
        }}
    />

    <span className="FLRadarAircraftLabel">
        {flight.name}
    </span>
</div>

    );
})}

    {aircraft.length === 0 && (
        <div className="FLRadarEmpty">
            No tracked aircraft
        </div>
    )}
</div>
        </div>
    );
}
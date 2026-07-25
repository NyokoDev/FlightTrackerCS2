interface FlightRadarProps {
    onClose: () => void;
}

export default function FlightRadar({
    onClose
}: FlightRadarProps) {
    return (
        <div className="flightTrackerRadar">

            {/* Your radar UI */}

            <button onClick={onClose}>
                ×
            </button>

        </div>
    );
}
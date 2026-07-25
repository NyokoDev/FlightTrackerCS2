import React, { useState } from "react";
import { createPortal } from "react-dom";
import { ModRegistrar } from "cs2/modding";
import FlightTrackerRadar from "./mods/MainUI/MainUI";
import FlightTrackerSVG from "./images/FlightTracker.svg";
import "./index.scss";

function FlightTrackerButton() {
    const [show, setShow] = useState(false);

    return (
        <>
            <button
    className="flight-tracker-button icon_be5 tinted-icon_iKo"
    style={{
        WebkitMaskImage: `url("${FlightTrackerSVG}")`,
        maskImage: `url("${FlightTrackerSVG}")`,
    }}
    onClick={() => setShow(true)}
>
</button>

            {show &&
                createPortal(
                    <FlightTrackerRadar
                        onClose={() => setShow(false)}
                        
                    />,
                    document.body
                )}
        </>
    );
}

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append(
        "UniversalModMenu",
        FlightTrackerButton
    );
};

export default register;
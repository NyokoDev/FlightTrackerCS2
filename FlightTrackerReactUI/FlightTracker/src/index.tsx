import React, { useState } from "react";
import { createPortal } from "react-dom";
import { trigger } from "cs2/api";
import { ModRegistrar } from "cs2/modding";

import FlightTrackerRadar from "./mods/MainUI/MainUI";
import FlightTrackerSVG from "./images/FlightTracker.svg";
import "./index.scss";
import { Button } from "cs2/ui";

function FlightTrackerButton() {
    const [show, setShow] = useState(false);

    const openTracker = () => {
        setShow(true);
        trigger("FlightTracker", "ToggleUIEnabled");
    };

    const closeTracker = () => {
        setShow(false);
        trigger("FlightTracker", "ToggleUIEnabled");
    };

    return (
        <>
            <Button
                src={FlightTrackerSVG}
                className="flight-tracker-button"
                variant="floating"
                onClick={openTracker}
            />
            

            {show &&
                createPortal(
                    <FlightTrackerRadar onClose={closeTracker} />,
                    document.body
                )}
        </>
    );
}

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append(
        "GameTopLeft",
        FlightTrackerButton
    );
};

export default register;
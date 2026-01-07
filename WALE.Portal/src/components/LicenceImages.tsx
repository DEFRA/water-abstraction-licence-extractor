interface LicenceImagesProps {
    filename: string;
}

export function LicenceImages({filename}: LicenceImagesProps) {
    return (
        <div className="licence-images">
            <img src={filename} alt="Licence"/>
        </div>
    );
}

export default LicenceImages;

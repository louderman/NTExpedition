from pathlib import Path
import unittest
from PIL import Image

class ScrewdriverSpriteTest(unittest.TestCase):
    def test_screwdriver_contains_handle_and_full_shaft(self):
        root = Path(__file__).resolve().parents[1]
        image = Image.open(root / 'Assets/Resources/FrontierInventory/screwdriver.png').convert('RGBA')
        bounds = image.getchannel('A').getbbox()
        if bounds is None:
            self.fail('sprite has no visible pixels')
            return
        self.assertGreaterEqual(bounds[2] - bounds[0], 24, 'sprite is missing its shaft')

if __name__ == '__main__':
    unittest.main()

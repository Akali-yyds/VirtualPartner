"""Input-boundary tests; no model, service, or credentials required."""
import unittest
from momask_playground import validate


class PlaygroundInputTests(unittest.TestCase):
    def job(self, **changes):
        value = dict(text='Raise the left hand.', useLlm=False, seconds=4, seed=11)
        value.update(changes)
        return value

    def test_direct_english_and_chinese_translation(self):
        validate(self.job())
        validate(self.job(text='举起左手', useLlm=True))

    def test_chinese_cannot_silently_reach_english_encoder(self):
        with self.assertRaises(ValueError):
            validate(self.job(text='举起左手'))

    def test_invalid_input_fails_before_generation(self):
        for changes in [dict(text=' '), dict(text='a'*2001), dict(seconds=3),
                        dict(seed=-1), dict(seed=True), dict(seed=2147483648)]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                validate(self.job(**changes))


if __name__ == '__main__':
    unittest.main()

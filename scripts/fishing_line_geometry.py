import math


SWING_CLIPS = ('cast', 'reel', 'reel_empty')


def swing_offset(clip, records, durations, index):
    record = records[index]
    horizontal = record['end'][0] - record['tip'][0]
    vertical = record['end'][1] - record['tip'][1]
    length = math.hypot(horizontal, vertical)
    if length < 1:
        return [0, 0]
    normal = [-vertical / length, horizontal / length]
    previous = max(0, index - 1)
    following = min(len(records) - 1, index + 1)
    seconds = max(.001, sum(durations[previous:following]) / 1000)
    velocity = [(records[following]['end'][axis] - records[previous]['end'][axis]) / seconds
                for axis in range(2)] if following != index else [0, 0]
    trailing = -(velocity[0] * normal[0] + velocity[1] * normal[1]) * .018
    gravity = 22 * normal[1]
    tension = .25 if clip == 'reel' else .7
    settling = 0 if following == index else 1
    displacement = (gravity + trailing * settling) * tension
    limit = min(28, length * (.08 if clip == 'reel' else .2))
    displacement = max(-limit, min(limit, displacement))
    return [normal[axis] * displacement for axis in range(2)]


def swing_line(clip, records, durations, index, following, amount, tip, endpoint):
    first = swing_offset(clip, records, durations, index)
    second = swing_offset(clip, records, durations, following)
    offset = [first[axis] + (second[axis] - first[axis]) * amount for axis in range(2)]
    control = [(tip[axis] + endpoint[axis]) / 2 + offset[axis] for axis in range(2)]
    return [*tip, *control, *endpoint]


def quadratic_points(line, steps=160):
    return [tuple((1 - amount) ** 2 * line[axis] + 2 * (1 - amount) * amount * line[axis + 2]
                  + amount ** 2 * line[axis + 4] for axis in range(2))
            for amount in (step / steps for step in range(steps + 1))]
